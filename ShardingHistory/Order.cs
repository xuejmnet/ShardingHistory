using System;

/*
* @Author: xjm
* @Description:
* @Date: DATE TIME
* @Email: 326308290@qq.com
*/
namespace ShardingHistory
{
    public class Order
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public OrderStatusEnum OrderStatus { get; set; }
        public DateTime? PayTime { get; set; }
        public DateTime CreateTime { get; set; }
    }

    public enum OrderStatusEnum
    {
        NoPay=1,
        Paid=1<<1
    }
}