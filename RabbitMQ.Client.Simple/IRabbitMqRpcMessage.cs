using System;
using System.Collections.Generic;
using System.Text;

namespace RabbitMQ.Client.Simple
{
    public interface IRabbitMqRpcMessage
    {
        string CorrelationId { get; set; }
        DateTime SentTime { get; set; }
        DateTime ValidTo { get; set; }

        void Timeout();
        void ExecuteCallback(IRabbitMQBodySerializer serializer, byte[] body);
    }
}
