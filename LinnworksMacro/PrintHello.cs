using LinnworksAPI;
using LinnworksMacroHelpers;

namespace LinnworksMacro
{
    public class PrintHello : LinnworksMacroBase
    {
        public void Execute(Guid[] orderIds)
        {
            Logger.WriteInfo("Hello from PrintHello macro");
            foreach (var orderid in orderIds)
            {
                ProcessingOrder(orderid);
            }

        }
        public void ProcessingOrder(Guid orderId)
        {
            var order = Api.Orders.GetOrderById(orderId);         
         
            if (order == null)
            {
                Logger.WriteError($"Order {orderId} not Found for this user");
                return;
            }

            Logger.WriteInfo($"Processing Order ID: {order.NumOrderId}");
            return;
        }
    }
}
