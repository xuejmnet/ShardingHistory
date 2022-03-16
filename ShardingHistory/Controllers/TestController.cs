using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ShardingHistory.Controllers;

[ApiController]
[Route("[controller]/[action]")]
public class TestController : ControllerBase
{
    private readonly MyDbContext _myDbContext;

    private readonly ILogger<TestController> _logger;

    public TestController(MyDbContext myDbContext)
    {
        _myDbContext = myDbContext;
    }

    public async Task<IActionResult> Test1()
    {
        var begin2016 = new DateTime(2016,1,1);
        var end2018 = new DateTime(2018,3,1);
        Console.WriteLine("------查询历史表Begin------");
        var result = await _myDbContext.Set<Order>().Where(o => o.CreateTime > begin2016 && o.CreateTime < end2018).ToListAsync();
        Console.WriteLine("------查询历史表End------");
        
        var end2019 = new DateTime(2019,3,3);
        Console.WriteLine("------查询历史表+2019表Begin------");
        var result1 = await _myDbContext.Set<Order>().Where(o => o.CreateTime > begin2016 && o.CreateTime < end2019).ToListAsync();
        Console.WriteLine("------查询历史表+2019表End------");
        
        Console.WriteLine("------查询订单根据id Begin------");
        var result2 = await _myDbContext.Set<Order>().Where(o => o.Id=="0d3a70e78b0a410097fe140bc5955065").ToListAsync();
        Console.WriteLine("------查询订单根据id End------");
        return Ok();
    }
}