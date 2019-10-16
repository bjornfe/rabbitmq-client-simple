using System;
using System.Collections.Generic;
using System.Text;

namespace RabbitMQ.Client.Simple
{
    public abstract class RabbitMQSubscription
    {
        /// <summary>
        /// Specify QueueName, if empty, RabbitMQ will create a queuename for you
        /// </summary>
        public string QueueName = "";

        /// <summary>
        /// Specify if the queue shall be durable (remain if the server is restarted) on the RabbitMQ broker. 
        /// </summary>
        public bool Durable = false;

        /// <summary>
        /// Specify if the queue shall be exclusive to this subscriber only. Normally true in combination with empty queuename
        /// </summary>
        public bool Exclusive = true;

        /// <summary>
        /// Spceify if the queue shall be deleted when the client disconnects from the broker.
        /// </summary>
        public bool AutoDelete = true;

        /// <summary>
        /// Specify if the received messages shall be autoacked against the broker. Normally this is true for ListenerSubscription and false for RpcSubscription.
        /// </summary>
        public bool AutoAck = true;

        /// <summary>
        /// Specify the routingkey for the subscription (ex. event.aliensarrived.from.mars) or with wildcard (ex. event.ailiensarrived.from.*)
        /// </summary>
        public string RoutingKey = "";

        /// <summary>
        /// Specify the Exchange used for distributing this message. Standard is amq.topic for topic messages and "" for messages targeting a queue directly.
        /// </summary>
        public string Exchange = "";

        /// <summary>
        /// Specify if failed (null result or exception) from the RPC Callback shall cause the message to be requeued in the broker.
        /// </summary>
        public bool AutoRequeue = false;
    }
}
