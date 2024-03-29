﻿namespace LogHost
{
    class LogDbContext : DbContext
    {
        public LogDbContext() { }
        public LogDbContext(DbContextOptions options) : base(options) { }

        public DbSet<LogModel> Log { get; set; } = null!; // use null-forgiving operator to prevent compiler warning 

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Filename=log.db");
            base.OnConfiguring(optionsBuilder);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Map table names
            modelBuilder.Entity<LogModel>().ToTable("Log");
            modelBuilder.Entity<LogModel>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.SentDt);
                entity.HasIndex(e => e.CustomerId);
            });
            base.OnModelCreating(modelBuilder);
        }
    }
}
