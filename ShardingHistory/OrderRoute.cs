using System;
using System.Globalization;
using System.Linq.Expressions;
using ShardingCore.Core.EntityMetadatas;
using ShardingCore.Core.VirtualRoutes;
using ShardingCore.Helpers;
using ShardingCore.VirtualRoutes.Months;

/*
* @Author: xjm
* @Description:
* @Date: DATE TIME
* @Email: 326308290@qq.com
*/
namespace ShardingHistory
{
    public class OrderRoute:AbstractSimpleShardingMonthKeyDateTimeVirtualTableRoute<Order>
    {
        public override void Configure(EntityMetadataTableBuilder<Order> builder)
        {
            builder.ShardingProperty(o => o.CreateTime);
            builder.ShardingExtraProperty(o => o.Id);
        }

        public override bool AutoCreateTableByTime()
        {
            return true;
        }

        public override DateTime GetBeginTime()
        {
            return new DateTime(2016, 1, 1);
        }
        //系统启动需要知道数据库应该有哪些表
        public override List<string> GetAllTails()
        {
            var tails=new List<string>();
            tails.Add("History");
            tails.Add("2019");
            tails.Add("2020");
            tails.Add("2021");
           
            var beginTime = ShardingCoreHelper.GetCurrentMonthFirstDay(new DateTime(2022,1,1));
         
            //提前创建表
            var nowTimeStamp =ShardingCoreHelper.GetCurrentMonthFirstDay(DateTime.Now);
            if (beginTime > nowTimeStamp)
                throw new ArgumentException("begin time error");
            var currentTimeStamp = beginTime;
            while (currentTimeStamp <= nowTimeStamp)
            {
                var tail = ShardingKeyToTail(currentTimeStamp);
                tails.Add(tail);
                currentTimeStamp = ShardingCoreHelper.GetNextMonthFirstDay(currentTimeStamp);
            }
            return tails;
        }

        private static readonly DateTime historyTime = new DateTime(2019, 1, 1);
        private static readonly DateTime yearTime = new DateTime(2022, 1, 1);
        protected override string TimeFormatToTail(DateTime shardingKey)
        {
            if (shardingKey < historyTime)
            {
                return "History";
            }

            if (shardingKey < yearTime)
            {
                return $"{shardingKey:yyyy}";
            }
            return base.TimeFormatToTail(shardingKey);
        }

        private static readonly HistoryMinComparer _historyMinComparer = new HistoryMinComparer();

        public override Expression<Func<string, bool>> GetRouteToFilter(DateTime shardingKey, ShardingOperatorEnum shardingOperator)
        {
            var t = TimeFormatToTail(shardingKey);
            switch (shardingOperator)
            {
                case ShardingOperatorEnum.GreaterThan:
                case ShardingOperatorEnum.GreaterThanOrEqual:
                    return tail => _historyMinComparer.Compare(tail, t) >= 0;
                case ShardingOperatorEnum.LessThan:
                {
                    // var currentMonth = ShardingCoreHelper.GetCurrentMonthFirstDay(shardingKey);
                    // //处于临界值 o=>o.time < [2021-01-01 00:00:00] 尾巴20210101不应该被返回
                    // if (currentMonth == shardingKey)
                    //     return tail => _historyMinComparer.Compare(tail, t) < 0;
                    return tail => _historyMinComparer.Compare(tail, t) <= 0;
                }
                case ShardingOperatorEnum.LessThanOrEqual:
                    return tail => _historyMinComparer.Compare(tail, t) <= 0;
                case ShardingOperatorEnum.Equal: return tail => tail == t;
                default:
                {
#if DEBUG
                    Console.WriteLine($"shardingOperator is not equal scan all table tail");
#endif
                    return tail => true;
                }
            }
        }

        public override Expression<Func<string, bool>> GetExtraRouteFilter(object shardingKey, ShardingOperatorEnum shardingOperator, string shardingPropertyName)
        {
            if (shardingPropertyName == nameof(Order.Id))
            {
                return GetOrderNoRouteFilter(shardingKey, shardingOperator);
            }
            return base.GetExtraRouteFilter(shardingKey, shardingOperator, shardingPropertyName);
        }
        /// <summary>
        /// 订单编号的路由
        /// </summary>
        /// <param name="shardingKey"></param>
        /// <param name="shardingOperator"></param>
        /// <returns></returns>
        private Expression<Func<string, bool>> GetOrderNoRouteFilter(object shardingKey,
            ShardingOperatorEnum shardingOperator)
        {
            //将分表字段转成订单编号
            var orderNo = shardingKey?.ToString() ?? string.Empty;
            //判断订单编号是否是我们符合的格式
            if (!CheckOrderNo(orderNo, out var orderTime))
            {
                //如果格式不一样就查询redis
                var t = RedisHelper.Get(shardingKey.ToString());
                if (string.IsNullOrWhiteSpace(t))
                {
                    return tail => false;
                }
                return tail => tail==t;
            }

            //当前时间的tail
            var currentTail = TimeFormatToTail(orderTime);
            //因为是按月分表所以获取下个月的时间判断id是否是在临界点创建的
            //var nextMonthFirstDay = ShardingCoreHelper.GetNextMonthFirstDay(DateTime.Now);//这个是错误的
            var nextMonthFirstDay = ShardingCoreHelper.GetNextMonthFirstDay(orderTime);
            if (orderTime.AddSeconds(10) > nextMonthFirstDay)
            {
                var nextTail = TimeFormatToTail(nextMonthFirstDay);
                return DoOrderNoFilter(shardingOperator, orderTime, currentTail, nextTail);
            }
            //因为是按月分表所以获取这个月月初的时间判断id是否是在临界点创建的
            //if (orderTime.AddSeconds(-10) < ShardingCoreHelper.GetCurrentMonthFirstDay(DateTime.Now))//这个是错误的
            if (orderTime.AddSeconds(-10) < ShardingCoreHelper.GetCurrentMonthFirstDay(orderTime))
            {
                //上个月tail
                var previewTail = TimeFormatToTail(orderTime.AddSeconds(-10));

                return DoOrderNoFilter(shardingOperator, orderTime, previewTail, currentTail);
            }

            return DoOrderNoFilter(shardingOperator, orderTime, currentTail, currentTail);

        }

        private Expression<Func<string, bool>> DoOrderNoFilter(ShardingOperatorEnum shardingOperator, DateTime shardingKey, string minTail, string maxTail)
        {
            switch (shardingOperator)
            {
                case ShardingOperatorEnum.Equal:
                    {
                        var isSame = minTail == maxTail;
                        if (isSame)
                        {
                            return tail => tail == minTail;
                        }
                        else
                        {
                            return tail => tail == minTail || tail == maxTail;
                        }
                    }
                default:
                    {
                        return tail => true;
                    }
            }
        }

        private bool CheckOrderNo(string orderNo, out DateTime orderTime)
        {
            //yyyyMMddHHmmss
            if (orderNo.Length == 14)
            {
                if (DateTime.TryParseExact(orderNo, "yyyyMMddHHmmss", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var parseDateTime))
                {
                    orderTime = parseDateTime;
                    return true;
                }
            }

            orderTime = DateTime.MinValue;
            return false;
        }
    }
    public class HistoryMinComparer:IComparer<string>
    {
        private const string History = "History";

        public int Compare(string? x, string? y)
        {
            if (!Object.Equals(x, y))
            {
                if (History.Equals(x))
                    return -1;
                if (History.Equals(y))
                    return 1;
            }
            return Comparer<string>.Default.Compare(x, y);
        }
    }
}