using Microsoft.EntityFrameworkCore;
using minimal_api_rate_limiting.Models;

namespace minimal_api_rate_limiting.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers { get; set; }
}
