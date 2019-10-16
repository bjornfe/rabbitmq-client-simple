using MessagePack;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace RabbitMQ.Client.Simple
{
    public class RabbitMQMessagePackSerializer : IRabbitMQBodySerializer
    {
        public byte[] GetBytes(object obj)
        {
            try
            {
                var json = JsonConvert.SerializeObject(obj);
                return MessagePackSerializer.FromJson(json);
            }
            catch
            {
                return null;
            }
           
        }

        public T GetObject<T>(byte[] body)
        {
            try
            {
                var json = MessagePackSerializer.ToJson(body);
                return JsonConvert.DeserializeObject<T>(json);
            }
            catch
            {
                return default(T);
            }
        }
    }
}
