using RabbitMQ.Client.Simple;
using System;
using System.Collections.Generic;
using System.Text;

namespace RabbitMQSimpleClientExample
{
    public class ExampleSubscription : RPCSubscription<ExampleSubscriptionObject>
    {
        public ExampleSubscription()
        {
            QueueName = typeof(ExampleSubscriptionObject).Name;
            Exchange = "";
            Exclusive = false;
            AutoAck = false;
            AutoDelete = false;
            Durable = true;

            Callback = Handle;
        }

        private ExampleRpcResult Handle(ExampleSubscriptionObject obj)
        {
            return new ExampleRpcResult()
            {
                ExampleResultMessage = obj.ObjectContent
            };
        }
    }
}
