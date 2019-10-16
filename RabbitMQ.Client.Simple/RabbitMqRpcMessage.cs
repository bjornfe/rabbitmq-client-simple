using System;
using System.Collections.Generic;
using System.Text;

namespace RabbitMQ.Client.Simple
{
    public class RabbitMqRpcMessage<T> : RabbitMQMessage, IRabbitMqRpcMessage
    {
        public string CorrelationId { get; set; }
        public DateTime SentTime { get; set; }
        public DateTime ValidTo { get; set; }

        public Action<T> ResponseCallback;

        public Action TimeoutCallback;

        public RabbitMqRpcMessage()
        {

        }

        public void ExecuteCallback(IRabbitMQBodySerializer serializer, byte[] body)
        {
            var obj = serializer.GetObject<T>(body);
            ResponseCallback?.Invoke(obj);
        }

        public void Timeout()
        {
            TimeoutCallback?.Invoke();
        }
    }
}
