using System;
using Microsoft.EntityFrameworkCore;
using ShardingCore.Core.VirtualRoutes.TableRoutes.RouteTails.Abstractions;
using ShardingCore.Sharding;
using ShardingCore.Sharding.Abstractions;

/*
* @Author: xjm
* @Description:
* @Date: DATE TIME
* @Email: 326308290@qq.com
*/
namespace ShardingHistory
{
    public class MyDbContext:AbstractShardingDbContext,IShardingTableDbContext
    {
        public MyDbContext(DbContextOptions<MyDbContext> options) : base(options)
        {
            
        }

        public IRouteTail RouteTail { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Order>(builder =>
            {
                builder.HasKey(o => o.Id);
                builder.Property(o => o.Id).HasMaxLength(50).IsRequired().IsUnicode(false);
                builder.Property(o => o.Title).HasMaxLength(50).IsRequired();
                builder.Property(o => o.Description).HasMaxLength(255).IsRequired();
                builder.Property(o => o.OrderStatus).HasConversion<int>();
                builder.ToTable(nameof(Order));
            });
        }
    }
}