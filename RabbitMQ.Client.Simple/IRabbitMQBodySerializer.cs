using System;
using System.Collections.Generic;
using System.Text;

namespace RabbitMQ.Client.Simple
{
    public interface IRabbitMQBodySerializer
    {
        byte[] GetBytes(object obj);
        T GetObject<T>(byte[] body);
    }
}
