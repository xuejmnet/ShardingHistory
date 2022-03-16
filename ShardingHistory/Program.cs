using Microsoft.EntityFrameworkCore;
using ShardingCore;
using ShardingCore.Bootstrapers;
using ShardingCore.Core.VirtualDatabase.VirtualTables;
using ShardingCore.Core.VirtualRoutes.TableRoutes;
using ShardingCore.TableExists;
using ShardingHistory;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
ILoggerFactory efLogger = LoggerFactory.Create(builder =>
{
    builder.AddFilter((category, level) => category == DbLoggerCategory.Database.Command.Name && level == LogLevel.Information).AddConsole();
});
builder.Services.AddControllers();
builder.Services.AddShardingDbContext<MyDbContext>()
    .AddEntityConfig(o =>
    {
        o.CreateShardingTableOnStart = true;
        o.EnsureCreatedWithOutShardingTable = true;
        o.AddShardingTableRoute<OrderRoute>();
    })
    .AddConfig(o =>
    {
        o.ConfigId = "c1";
        o.UseShardingQuery((conStr, b) =>
        {
            b.UseMySql(conStr, new MySqlServerVersion(new Version())).UseLoggerFactory(efLogger);
        });
        o.UseShardingTransaction((conn, b) =>
        {
            b.UseMySql(conn, new MySqlServerVersion(new Version())).UseLoggerFactory(efLogger);
        });
        o.AddDefaultDataSource("ds0", "server=127.0.0.1;port=3306;database=ShardingHistoryDB;userid=root;password=root;");
        o.ReplaceTableEnsureManager(sp => new MySqlTableEnsureManager<MyDbContext>());
    }).EnsureConfig();

var app = builder.Build();
RedisHelper.Initialization(new CSRedis.CSRedisClient("127.0.0.1:6379,defaultDatabase=0,poolsize=10,ssl=false,writeBuffer=10240"));

app.Services.GetRequiredService<IShardingBootstrapper>().Start();
using (var scope = app.Services.CreateScope())
{
    var myDbContext = scope.ServiceProvider.GetRequiredService<MyDbContext>();
    if (!myDbContext.Set<Order>().Any())
    {
        List<Order> orders = new List<Order>();
        var order2016s = createOrders(2016,50);
        var order2017s = createOrders(2017,100);
        var order2018s = createOrders(2018,200);
        var order2019s = createOrders(2019,300);
        var order2020s = createOrders(2020,300);
        var order2021s = createOrders(2021,300);
        var order2022s = createOrders(2022,90);
        orders.AddRange(order2016s);
        orders.AddRange(order2017s);
        orders.AddRange(order2018s);
        orders.AddRange(order2019s);
        orders.AddRange(order2020s);
        orders.AddRange(order2021s);
        orders.AddRange(order2022s);
        
        myDbContext.AddRange(orders);
        myDbContext.SaveChanges();
        var virtualTableManager = app.Services.GetRequiredService<IVirtualTableManager<MyDbContext>>();
        var virtualTable = virtualTableManager.GetVirtualTable(typeof(Order));
        foreach (var order in orders.Where(o=>o.CreateTime<new DateTime(2022,1,1)))
        {
            var physicTables = virtualTable.RouteTo(new ShardingTableRouteConfig(shardingKeyValue:order.CreateTime));
            var tail = physicTables[0].Tail;
            RedisHelper.Set(order.Id, tail);
        }
    }
}
app.MapControllers();

app.Run();

List<Order> createOrders(int year,int count)
{
    var beginTime = new DateTime(year, 1, 1, 1, 1,1);
    var orders = Enumerable.Range(1,count)
        .Select((o, i) =>
        {
            var createTime = beginTime.AddDays(i);
            return new Order()
            {
                Id = year<2022?Guid.NewGuid().ToString("n"):$"{createTime:yyyyMMddHHmmss}",
                CreateTime = createTime,
                Title = year+"年订单:" + i,
                Description = year+"年订单详细描述:" + i,
                OrderStatus = i % 7 == 0 ? OrderStatusEnum.NoPay : OrderStatusEnum.Paid,
                PayTime = i % 7 == 0 ? null : createTime.AddSeconds(new Random().Next(1, 300)),
            };
        }).ToList();
    return orders;
}