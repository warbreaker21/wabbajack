using System;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.BuildServer;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.NexusApi;
using Wabbajack.Server.DataLayer;
using Wabbajack.Server.DTOs;

namespace Wabbajack.Server.Services
{
    public class ArchiveDownloader : AbstractService<ArchiveDownloader, int>
    {
        private SqlService _sql;
        private ArchiveMaintainer _archiveMaintainer;
        private NexusApiClient _nexusClient;
        private DiscordWebHook _discord;

        public ArchiveDownloader(ILogger<ArchiveDownloader> logger, AppSettings settings, SqlService sql, ArchiveMaintainer archiveMaintainer, DiscordWebHook discord, QuickSync quickSync) 
            : base(logger, settings, quickSync, TimeSpan.FromMinutes(10))
        {
            _sql = sql;
            _archiveMaintainer = archiveMaintainer;
            _discord = discord;
        }

        public override async Task<int> Execute()
        {
            _nexusClient ??= await NexusApiClient.Get();
            int count = 0;

            while (true)
            {
                var (daily, hourly) = await _nexusClient.GetRemainingApiCalls();
                bool ignoreNexus = (daily < 100 && hourly < 10);
                //var ignoreNexus = true;
                if (ignoreNexus)
                    _logger.LogWarning($"Ignoring Nexus Downloads due to low hourly api limit (Daily: {daily}, Hourly:{hourly})");
                else
                    _logger.LogInformation($"Looking for any download (Daily: {_nexusClient.DailyRemaining}, Hourly:{_nexusClient.HourlyRemaining})");

                var nextDownload = await _sql.GetNextPendingDownload(ignoreNexus);

                if (nextDownload == null)
                    break;
                
                _logger.LogInformation($"Checking for previously archived {nextDownload.Hash}");
                
                if (nextDownload.Hash != default && _archiveMaintainer.HaveArchive(nextDownload.Hash.Value))
                {
                    await nextDownload.Finish(_sql.Context);
                    continue;
                }

                if (nextDownload.DownloadState is ManualDownloader.State)
                {
                    await nextDownload.Finish(_sql.Context);
                    continue;
                }

                try
                {
                    _logger.Log(LogLevel.Information, $"Downloading {nextDownload.PrimaryKeyString}");
                    if (!(nextDownload.DownloadState is GameFileSourceDownloader.State)) 
                        await _discord.Send(Channel.Spam, new DiscordMessage {Content = $"Downloading {nextDownload.PrimaryKeyString}"});
                    await DownloadDispatcher.PrepareAll(new[] {nextDownload.DownloadState});

                    await using var tempPath = new TempFile();
                    await nextDownload.DownloadState.Download(nextDownload.ToArchive(), tempPath.Path);

                    var hash = await tempPath.Path.FileHashAsync();
                    
                    if (nextDownload.Hash != default && hash != nextDownload.Hash)
                    {
                        _logger.Log(LogLevel.Warning, $"Downloaded archive hashes don't match for {nextDownload.PrimaryKeyString} {nextDownload.Hash} {nextDownload.Size} vs {hash} {tempPath.Path.Size}");
                        await nextDownload.Fail(_sql.Context, "Invalid Hash");
                        continue;
                    }

                    if (nextDownload.Size != default &&
                        tempPath.Path.Size != nextDownload.Size)
                    {
                        await nextDownload.Fail(_sql.Context, "Invalid Size");
                        continue;
                    }
                    nextDownload.Hash = hash;
                    nextDownload.Size = tempPath.Path.Size;

                    _logger.Log(LogLevel.Information, $"Archiving {nextDownload.PrimaryKeyString}");
                    await _archiveMaintainer.Ingest(tempPath.Path);

                    _logger.Log(LogLevel.Information, $"Finished Archiving {nextDownload.PrimaryKeyString}");
                    await nextDownload.Finish(_sql.Context);
                    
                    if (!(nextDownload.DownloadState is GameFileSourceDownloader.State)) 
                        await _discord.Send(Channel.Spam, new DiscordMessage {Content = $"Finished downloading {nextDownload.PrimaryKeyString}"});


                }
                catch (Exception ex)
                {
                    _logger.Log(LogLevel.Warning, $"Error downloading {nextDownload.PrimaryKeyString}");
                    await nextDownload.Fail(_sql.Context, ex.ToString());
                    await _discord.Send(Channel.Spam, new DiscordMessage {Content = $"Error downloading {nextDownload.PrimaryKeyString}"});
                }
                
                count++;
            }

            if (count > 0)
            {
                // Wake the Patch builder up in case it needs to build a patch now
                await _quickSync.Notify<PatchBuilder>();
            }

            return count;
        }
    }
}
