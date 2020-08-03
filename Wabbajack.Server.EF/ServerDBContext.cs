using System;
using System.Xml;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Wabbajack.Common;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.NexusApi;

#nullable disable

namespace Wabbajack.Server.EF
{
    public partial class ServerDBContext : DbContext
    {
        private string _connString;

        public ServerDBContext()
        {
        }

        public ServerDBContext(DbContextOptions<ServerDBContext> options)
            : base(options)
        {
        }

        public ServerDBContext(string conn)
        {
            _connString = conn;
        }

        public virtual DbSet<AccessLog> AccessLogs { get; set; }
        public virtual DbSet<AllArchiveContent> AllArchiveContents { get; set; }
        public virtual DbSet<AllFilesInArchive> AllFilesInArchives { get; set; }
        public virtual DbSet<ApiKey> ApiKeys { get; set; }
        public virtual DbSet<ArchiveContent> ArchiveContents { get; set; }
        public virtual DbSet<ArchiveDownload> ArchiveDownloads { get; set; }
        public virtual DbSet<ArchivePatch> ArchivePatches { get; set; }
        public virtual DbSet<AuthoredFile> AuthoredFiles { get; set; }
        public virtual DbSet<AuthoredFilesSummary> AuthoredFilesSummaries { get; set; }
        public virtual DbSet<IndexedFile> IndexedFiles { get; set; }
        public virtual DbSet<Job> Jobs { get; set; }
        public virtual DbSet<Metric> Metrics { get; set; }
        public virtual DbSet<MirroredArchive> MirroredArchives { get; set; }
        public virtual DbSet<ModList> ModLists { get; set; }
        public virtual DbSet<ModListArchive> ModListArchives { get; set; }
        public virtual DbSet<ModListArchiveStatus> ModListArchiveStatuses { get; set; }
        public virtual DbSet<NexusFileInfo> NexusFileInfos { get; set; }
        public virtual DbSet<NexusKey> NexusKeys { get; set; }
        public virtual DbSet<NexusModFile> NexusModFiles { get; set; }
        public virtual DbSet<NexusModFilesSlow> NexusModFilesSlows { get; set; }
        public virtual DbSet<NexusModInfo> NexusModInfos { get; set; }
        public virtual DbSet<NexusModPermission> NexusModPermissions { get; set; }
        public virtual DbSet<Patch> Patches { get; set; }
        public virtual DbSet<TarKey> TarKeys { get; set; }
        public virtual DbSet<UploadedFile> UploadedFiles { get; set; }
        public virtual DbSet<VirusScanResult> VirusScanResults { get; set; }
        
        // Virtual entities (created by direct SQL queries)
        public virtual DbSet<AggregateMetric> AggregateMetrics { get; set; }
        
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see http://go.microsoft.com/fwlink/?LinkId=723263.
                optionsBuilder.UseSqlServer(_connString);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AccessLog>(entity =>
            {
                entity.ToTable("AccessLog");

                entity.HasIndex(x => x.Ip, "AccessLogByIP");

                entity.HasIndex(x => x.MetricsKey, "AccessLogByMetricsKey");

                entity.Property(e => e.Action).IsRequired();

                entity.Property(e => e.Ip)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(e => e.MetricsKey)
                    .HasMaxLength(4000)
                    .HasComputedColumnSql("(json_value([Action],'$.Headers.\"x-metrics-key\"[0]'))", false);

                entity.Property(e => e.Timestamp)
                    .HasColumnType("datetime")
                    .HasAnnotation("Relational:ColumnType", "datetime");
            });

            modelBuilder.Entity<AllArchiveContent>(entity =>
            {
                entity.HasNoKey();

                entity.ToView("AllArchiveContent");
            });

            modelBuilder.Entity<AllFilesInArchive>(entity =>
            {
                entity.HasKey(x => new { x.TopParent, x.Child });

                entity.ToTable("AllFilesInArchive");

                entity.HasIndex(x => x.Child, "IX_Child");
            });

            modelBuilder.Entity<ApiKey>(entity =>
            {
                entity.HasKey(x => new { x.Apikey1, x.Owner });

                entity.HasIndex(x => new { x.Owner, x.Apikey1 }, "ByAPIKey")
                    .IsUnique();

                entity.Property(e => e.Apikey1)
                    .HasMaxLength(260)
                    .HasColumnName("APIKey");

                entity.Property(e => e.Owner).HasMaxLength(40);
            });

            modelBuilder.Entity<ArchiveContent>(entity =>
            {
                entity.HasNoKey();

                entity.ToTable("ArchiveContent");

                entity.HasIndex(x => new { x.Child, x.Parent }, "Child_Parent_IDX")
                    .IsClustered();

                entity.HasIndex(x => x.Child, "IX_ArchiveContent_Child");

                entity.HasIndex(x => new { x.Parent, x.PathHash }, "PK_ArchiveContent")
                    .IsUnique();

                entity.Property(e => e.PathHash)
                    .IsRequired()
                    .HasMaxLength(32)
                    .HasComputedColumnSql("(CONVERT([binary](32),hashbytes('SHA2_256',[Path])))", true)
                    .IsFixedLength(true);
            });

            modelBuilder.Entity<ArchiveDownload>(entity =>
            {
                entity.HasIndex(x => new { x.DownloadFinished, x.Downloader }, "ByDownloaderAndFinished");

                entity.HasIndex(x => new { x.PrimaryKeyString, x.Hash }, "ByPrimaryKeyAndHash");

                entity.Property(e => e.Id).ValueGeneratedNever();

                entity.Property(e => e.Hash)
                    .HasConversion(e => e == default ? null : (long?)e, e => e == null ? Hash.Empty : Hash.FromLong(e.Value));

                entity.Property(e => e.DownloadFinished)
                    .HasColumnType("datetime")
                    .HasAnnotation("Relational:ColumnType", "datetime");

                entity.Property(e => e.IsFailed)
                    .HasColumnType("tinyint")
                    .HasConversion(e => e.Value ? 1 : 0, i => i == 1);

                entity.Property(e => e.DownloadState)
                    .HasConversion(e => e.ToJson(false), e => e.FromJsonString<AbstractDownloadState>())
                    .IsRequired();

                entity.Property(e => e.Downloader)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(e => e.PrimaryKeyString)
                    .IsRequired()
                    .HasMaxLength(255);
            });

            modelBuilder.Entity<ArchivePatch>(entity =>
            {
                entity.HasKey(x => new { x.SrcPrimaryKeyStringHash, x.SrcHash, x.DestPrimaryKeyStringHash, x.DestHash });

                entity.Property(e => e.SrcPrimaryKeyStringHash)
                    .HasMaxLength(32)
                    .IsFixedLength(true);

                entity.Property(e => e.DestPrimaryKeyStringHash)
                    .HasMaxLength(32)
                    .IsFixedLength(true);

                entity.Property(e => e.Cdnpath).HasColumnName("CDNPath");

                entity.Property(e => e.DestPrimaryKeyString).IsRequired();

                entity.Property(e => e.DestState).IsRequired();

                entity.Property(e => e.SrcPrimaryKeyString).IsRequired();

                entity.Property(e => e.SrcState).IsRequired();
            });

            modelBuilder.Entity<AuthoredFile>(entity =>
            {
                entity.HasKey(x => x.ServerAssignedUniqueId);

                entity.Property(e => e.ServerAssignedUniqueId).ValueGeneratedNever();

                entity.Property(e => e.CdnfileDefinition)
                    .IsRequired()
                    .HasColumnName("CDNFileDefinition");

                entity.Property(e => e.Finalized)
                    .HasColumnType("datetime")
                    .HasAnnotation("Relational:ColumnType", "datetime");

                entity.Property(e => e.LastTouched)
                    .HasColumnType("datetime")
                    .HasAnnotation("Relational:ColumnType", "datetime");
            });

            modelBuilder.Entity<AuthoredFilesSummary>(entity =>
            {
                entity.HasNoKey();

                entity.ToView("AuthoredFilesSummaries");

                entity.Property(e => e.Author).HasMaxLength(4000);

                entity.Property(e => e.Finalized)
                    .HasColumnType("datetime")
                    .HasAnnotation("Relational:ColumnType", "datetime");

                entity.Property(e => e.LastTouched)
                    .HasColumnType("datetime")
                    .HasAnnotation("Relational:ColumnType", "datetime");

                entity.Property(e => e.MungedName).HasMaxLength(4000);

                entity.Property(e => e.OriginalFileName).HasMaxLength(4000);

                entity.Property(e => e.Size).HasMaxLength(4000);
            });

            modelBuilder.Entity<IndexedFile>(entity =>
            {
                entity.HasKey(x => x.Hash);

                entity.ToTable("IndexedFile");

                entity.HasIndex(x => x.Sha256, "IX_IndexedFile_By_SHA256")
                    .IsUnique();

                entity.Property(e => e.Hash).ValueGeneratedNever();

                entity.Property(e => e.Md5)
                    .IsRequired()
                    .HasMaxLength(16)
                    .IsFixedLength(true);

                entity.Property(e => e.Sha1)
                    .IsRequired()
                    .HasMaxLength(20)
                    .IsFixedLength(true);

                entity.Property(e => e.Sha256)
                    .IsRequired()
                    .HasMaxLength(32)
                    .IsFixedLength(true);
            });

            modelBuilder.Entity<Job>(entity =>
            {
                entity.HasNoKey();

                entity.Property(e => e.Created)
                    .HasColumnType("datetime")
                    .HasAnnotation("Relational:ColumnType", "datetime");

                entity.Property(e => e.Ended)
                    .HasColumnType("datetime")
                    .HasAnnotation("Relational:ColumnType", "datetime");

                entity.Property(e => e.Id).ValueGeneratedOnAdd();

                entity.Property(e => e.Started)
                    .HasColumnType("datetime")
                    .HasAnnotation("Relational:ColumnType", "datetime");
            });

            modelBuilder.Entity<Metric>(entity =>
            {
                entity.Property(e => e.Action)
                    .IsRequired()
                    .HasMaxLength(64);

                entity.Property(e => e.GroupingSubject).HasComputedColumnSql("(substring([Subject],(0),case when patindex('%[0-9].%',[Subject])=(0) then len([Subject])+(1) else patindex('%[0-9].%',[Subject]) end))", false);

                entity.Property(e => e.MetricsKey).HasMaxLength(64);

                entity.Property(e => e.Subject).IsRequired();

                entity.Property(e => e.Timestamp)
                    .HasColumnType("datetime")
                    .HasAnnotation("Relational:ColumnType", "datetime");
            });

            modelBuilder.Entity<MirroredArchive>(entity =>
            {
                entity.HasKey(x => x.Hash);

                entity.Property(e => e.Hash).ValueGeneratedNever();

                entity.Property(e => e.Created)
                    .HasColumnType("datetime")
                    .HasAnnotation("Relational:ColumnType", "datetime");

                entity.Property(e => e.Rationale).IsRequired();

                entity.Property(e => e.Uploaded)
                    .HasColumnType("datetime")
                    .HasAnnotation("Relational:ColumnType", "datetime");
            });

            modelBuilder.Entity<ModList>(entity =>
            {
                entity.HasKey(x => x.MachineUrl);

                entity.Property(e => e.MachineUrl)
                    .HasMaxLength(50)
                    .HasColumnName("MachineURL");

                entity.Property(e => e.Metadata).IsRequired();

                entity.Property(e => e.Modlist1)
                    .IsRequired()
                    .HasColumnName("Modlist");
            });

            modelBuilder.Entity<ModListArchive>(entity =>
            {
                entity.HasKey(x => new { x.MachineUrl, x.Hash })
                    .HasName("PK_ModListArchive");

                entity.Property(e => e.MachineUrl).HasMaxLength(50);

                entity.Property(e => e.PrimaryKeyString).IsRequired();

                entity.Property(e => e.State).IsRequired();
            });

            modelBuilder.Entity<ModListArchiveStatus>(entity =>
            {
                entity.HasKey(x => new { x.PrimaryKeyStringHash, x.Hash });

                entity.ToTable("ModListArchiveStatus");

                entity.Property(e => e.PrimaryKeyStringHash)
                    .HasMaxLength(32)
                    .IsFixedLength(true);

                entity.Property(e => e.PrimaryKeyString).IsRequired();
            });

            modelBuilder.Entity<NexusFileInfo>(entity =>
            {
                entity.HasKey(x => new { x.Game, x.ModId, x.FileId });

                entity.Property(e => e.Data).IsRequired();

                entity.Property(e => e.LastChecked)
                    .HasColumnType("datetime")
                    .HasAnnotation("Relational:ColumnType", "datetime");
            });

            modelBuilder.Entity<NexusKey>(entity =>
            {
                entity.HasKey(x => x.ApiKey);

                entity.Property(e => e.ApiKey).HasMaxLength(162);
            });

            modelBuilder.Entity<NexusModFile>(entity =>
            {
                entity.HasKey(x => new { x.Game, x.ModId });
                
                entity.Property(e => e.Game).HasConversion(
                    v => (int)v.MetaData().NexusGameId,
                    v => GameRegistry.ByNexusID[v]);

                entity.Property(e => e.Data)
                    .IsRequired()
                    .HasConversion(e => e.ToJson(true),
                        e => e.FromJsonString<NexusApiClient.GetModFilesResponse>());

                entity.Property(e => e.LastChecked)
                    .HasColumnType("datetime")
                    .HasAnnotation("Relational:ColumnType", "datetime");
            });

            modelBuilder.Entity<NexusModFilesSlow>(entity =>
            {
                entity.HasKey(x => new { x.GameId, x.FileId, x.ModId });
                
                entity.ToTable("NexusModFilesSlow");

                entity.Property(e => e.LastChecked)
                    .HasColumnType("datetime")
                    .HasAnnotation("Relational:ColumnType", "datetime");
            });

            modelBuilder.Entity<NexusModInfo>(entity =>
            {
                entity.HasKey(x => new { x.Game, x.ModId });
                entity.Property(e => e.Game).HasConversion(
                    v => (int)v.MetaData().NexusGameId,
                    v => GameRegistry.ByNexusID[v]);

                entity.Property(e => e.Data)
                    .IsRequired()
                    .HasConversion(v => v.ToJson(true),
                        v => v.FromJsonString<ModInfo>());
                

                entity.Property(e => e.LastChecked)
                    .HasConversion(e => e, e => DateTime.SpecifyKind(e, DateTimeKind.Utc))
                    .HasColumnType("datetime")
                    .HasAnnotation("Relational:ColumnType", "datetime");
            });

            modelBuilder.Entity<NexusModPermission>(entity =>
            {
                entity.HasKey(x => new { x.NexusGameId, x.ModId });

                entity.Property(e => e.NexusGameId)
                    .HasColumnName("NexusGameID")
                    .HasConversion(
                    v => (int)v.MetaData().NexusGameId,
                    v => GameRegistry.ByNexusID[v]);

                entity.Property(e => e.ModId)
                    .HasColumnName("ModID");
               
            });

            modelBuilder.Entity<Patch>(entity =>
            {
                entity.HasKey(x => new { x.SrcId, x.DestId });

                entity.Property(e => e.FailMessage).IsUnicode(false);

                entity.Property(e => e.Finished)
                    .HasColumnType("datetime")
                    .HasAnnotation("Relational:ColumnType", "datetime");

                entity.Property(e => e.LastUsed)
                    .HasColumnType("datetime")
                    .HasAnnotation("Relational:ColumnType", "datetime");

                entity.HasOne(d => d.Dest)
                    .WithMany(p => p.PatchDests)
                    .HasForeignKey(x => x.DestId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_DestId");

                entity.HasOne(d => d.Src)
                    .WithMany(p => p.PatchSrcs)
                    .HasForeignKey(x => x.SrcId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_SrcId");
            });

            modelBuilder.Entity<TarKey>(entity =>
            {
                entity.HasKey(x => x.MetricsKey);

                entity.ToTable("TarKey");

                entity.Property(e => e.MetricsKey).HasMaxLength(64);
            });

            modelBuilder.Entity<UploadedFile>(entity =>
            {
                entity.Property(e => e.Id).ValueGeneratedNever();

                entity.Property(e => e.Cdnname).HasColumnName("CDNName");

                entity.Property(e => e.Name).IsRequired();

                entity.Property(e => e.UploadDate)
                    .HasColumnType("datetime")
                    .HasAnnotation("Relational:ColumnType", "datetime");

                entity.Property(e => e.UploadedBy)
                    .IsRequired()
                    .HasMaxLength(40);
            });

            modelBuilder.Entity<VirusScanResult>(entity =>
            {
                entity.HasKey(x => x.Hash);

                entity.Property(e => e.Hash).ValueGeneratedNever();
            });
            
            // Virtual Entities

            modelBuilder.Entity<AggregateMetric>(entity =>
            {
                entity.HasKey(x => new {x.Date, x.Subject});
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
