using System;
using System.Collections.Generic;
using System.Text;

namespace RabbitMQ.Client.Simple
{
    public class RabbitMQMessage
    {
        public string Exchange = "amq.topic";
        public string RoutingKey = "";
        public object content = null;
    }
}
