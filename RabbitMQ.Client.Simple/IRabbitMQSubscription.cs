using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace RabbitMQ.Client.Simple
{
    public interface IRabbitMQSubscription
    {
        event Action Unsubscribed;
        void Subscribe(IConnection connection, IRabbitMQBodySerializer serializer, CancellationToken stoppingToken);
        void Unsubscribe();
    }
}
