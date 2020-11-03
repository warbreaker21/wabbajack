using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.Lib.Http
{
    public class Downloader
    {
        private const long MinChunkSize = 1024 * 1024 * 128; // 128MB
        private const long MaxRetries = 10;
        
        private Client _client;
        private Uri _uri;
        private long _size;
        private AbsolutePath _destination;
        private FileStream? _fileStream;
        private AsyncLock _lock;
        private WorkQueue _workQueue;
        private int _numberOfChunks;

        public Downloader(Client client, Uri uri, long size, WorkQueue queue, AbsolutePath destination)
        {
            _client = client;
            _uri = uri;
            _size = size;
            _destination = destination;
            _lock = new AsyncLock();
            _workQueue = queue;
        }
        public static async Task<HttpResponseHeaders> GetDownloadHeaders(Client client, Uri uri)
        {
            var msg = new HttpRequestMessage {Method = HttpMethod.Head, RequestUri = uri};
            using var response = await client.SendAsync(msg, errorsAsExceptions:false);
            if (response.IsSuccessStatusCode) return response.Headers;

            using var response2 = await client.GetAsync(uri, errorsAsExceptions: true);
            return response2.Headers;
        }

        public async Task<bool> SupportsResume(Client client, Uri uri)
        {
            var headers = await GetDownloadHeaders(client, uri);
            return headers.AcceptRanges.FirstOrDefault(f => f == "bytes") != null;
        }

        public async Task<bool> Download()
        {
            _fileStream = await _destination.Create();

            if (_size < MinChunkSize)
            {
                _numberOfChunks = 1;
                await DownloadPart(0, 0, _size);
            }

            var numberOfChunks = _workQueue.DesiredNumWorkers;
            var chunkSize = _size / numberOfChunks;
            await Enumerable.Range(0, numberOfChunks)
                .PMap(_workQueue, async idx =>
                {
                    var start = idx * chunkSize;
                    var end = Math.Min(start + chunkSize, _size);
                    await DownloadPart(idx, start, end);
                });

            await _fileStream.DisposeAsync();
            return true;
        }

        private async Task DownloadPart(int part, long start, long end)
        {
            int retries = 0;
            TOP:
            long completed = 0;
            var msg = new HttpRequestMessage
            {
                Method = HttpMethod.Get, 
                RequestUri = _uri
            };
            
            if (start != 0 || end != _size) 
                msg.Headers.Range = new RangeHeaderValue(start, end);

            var response = await _client.SendAsync(msg);

            try
            {
                await using var data = await response.Content.ReadAsStreamAsync();
                byte[] buffer = new byte[1024 * 8];
                while (completed < end - start)
                {

                    var pcent = Percent.FactoryPutInRange(completed, end - start);
                    Utils.Status($"Downloading {_destination.FileName} Part ({part} of {_numberOfChunks})", pcent);
                    int read = data.Read(buffer, 0, buffer.Length);
                    if (read <= 0)
                        throw new InvalidDataException();

                    using var _ = await _lock.WaitAsync();
                    _fileStream!.Position = start + completed;
                    await _fileStream!.WriteAsync(buffer, 0, read);
                    await _fileStream.FlushAsync();
                    completed += read;
                }
                response.Dispose();

            }
            catch
            {
                response.Dispose();
                if (retries >= MaxRetries) throw;

                Utils.Log($"Stream error while downloading retrying {retries} of {MaxRetries}");
                retries += 1;
                start += completed;
                goto TOP;
            }

        }
    }
}
