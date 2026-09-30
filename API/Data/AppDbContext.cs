using Microsoft.EntityFrameworkCore;
using API.Model;
namespace API.Data
{
    public class AppDbContext: DbContext
    {

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {

        }

        public DbSet <UserAuth> UserAuths { get; set; }
        public string Email { get; internal set; }
        public DbSet <Transaction>Transactionss { get; set; }    
        public DbSet<Wallet>Wallets { get; set; }
        public DbSet<IdempotencyRecord> IdempotencyRecords { get; set; }



        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Tells PostgreSQL to generate a random UUID v4 upon INSERT
            modelBuilder.Entity<UserAuth>()
                .Property(u => u.Id)
                .HasDefaultValueSql("gen_random_uuid()");

            modelBuilder.Entity<Wallet>()
                .Property(w => w.Id)
                .HasDefaultValueSql("gen_random_uuid()");

            // Relationships
            modelBuilder.Entity<UserAuth>()
                .HasOne(x => x.Wallet)
                .WithOne(p => p.UserAuth)
                .HasForeignKey<Wallet>(p => p.UserAuthId);

            modelBuilder.Entity<UserAuth>()
                .HasIndex(x => x.Email)
                .IsUnique();

            modelBuilder.Entity<Wallet>()
                .HasIndex(x => x.Reference)
                .IsUnique();


            modelBuilder.Entity<IdempotencyRecord>()
       .HasIndex(x => new
       {
           x.UserId,
           x.Method,
           x.Path,
           x.Key
       })
       .IsUnique();

        }





    }
}
