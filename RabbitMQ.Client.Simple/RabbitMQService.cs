using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RabbitMQ.Client.Simple
{
    public class RabbitMQService
    {

        private IConnection connection;
        private IModel publish_channel;

        private IModel response_channel;
        private string responseQueueName;

        private RabbitMQOptions options;
        private ConnectionFactory connection_factory;
        private BlockingCollection<RabbitMQMessage> publish_queue = new BlockingCollection<RabbitMQMessage>();
        private CancellationTokenSource stopSubscriptionsTokenSource = new CancellationTokenSource();
        private CancellationToken stoppingToken;

        public IRabbitMQBodySerializer BodySerializer;

        public event Action Connected;
        public event Action<string> DebugText;

        private ConcurrentDictionary<string, IRabbitMqRpcMessage> rpc_queue = new ConcurrentDictionary<string, IRabbitMqRpcMessage>(); 


        public RabbitMQService(RabbitMQOptions options)
        {
            this.options = options;
        }

        public void Run(CancellationToken stoppingToken)
        {
            this.stoppingToken = stoppingToken;
            Task.Run(async () =>
            {
                while ((!verify_publish_channel() || !verify_response_channel()) && !stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(1000, stoppingToken);
                }

                if (stoppingToken.IsCancellationRequested)
                    return;

                Connected?.Invoke();

                
                while(!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        var msg = publish_queue.Take(stoppingToken);
                        if(publish_channel == null || !publish_channel.IsOpen || !connection.IsOpen)
                        {
                            publish_queue.Add(msg);
                            await Task.Delay(500,stoppingToken);
                            continue;
                        }
                        try
                        {
                            IBasicProperties props = null;
                            if (msg is IRabbitMqRpcMessage rpcMsg &&  rpcMsg.CorrelationId != null)
                            {
                                props = publish_channel.CreateBasicProperties();
                                props.CorrelationId = rpcMsg.CorrelationId;
                                props.ReplyTo = responseQueueName;
                            }

                            var body = BodySerializer.GetBytes(msg.content);

                            publish_channel.BasicPublish(
                                            exchange: msg.Exchange,
                                            routingKey: msg.RoutingKey,
                                            body: body,
                                            basicProperties:props
                                        );
                        }
                        catch(Exception err)
                        {
                            Console.WriteLine("Failed to publish -> " + err.ToString());
                            publish_queue.Add(msg);
                            await Task.Delay(500, stoppingToken);
                        }



                    }
                    catch(Exception err)
                    {
                        Console.WriteLine("General publish error -> " + err.ToString());
                    }
                    

                }

                stoppingToken.Register(() =>
                {
                    if (publish_channel != null && publish_channel.IsOpen)
                        publish_channel.Close();

                    stopSubscriptionsTokenSource.Cancel();

                    if (connection != null)
                        connection.Close();
                });

            },stoppingToken);

            //Houskeeping for rpc_queue. Flush messages that has timed out.
            Task.Run(async() =>
            {
                while(!stoppingToken.IsCancellationRequested)
                {
                    while(rpc_queue.Count() > 0 && rpc_queue.Values.Any(v=> DateTime.Now > v.ValidTo)) 
                    {
                        var entry = rpc_queue.Where(v => DateTime.Now > v.Value.ValidTo).First().Value;
                        entry.Timeout();
                        rpc_queue.TryRemove(entry.CorrelationId, out var val);
                    }

                    await Task.Delay(500, stoppingToken);
                }
            },stoppingToken);

        }

        private object connectionLock = new object();

        private bool check_connection()
        {

            lock (connectionLock)
            {
                if (connection != null && connection.IsOpen)
                    return true;
                else if (connection != null && !connection.IsOpen)
                    return false;

                try
                {
                    if (connection_factory == null)
                    {
                        try
                        {
                            connection_factory = new ConnectionFactory
                            {
                                UserName = options.Username,
                                Password = options.Password,
                                HostName = options.Hostname,
                                VirtualHost = options.VirtualHost,
                                NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
                                Port = options.Port,

                            };

                            if (options.UseSSL)
                            {
                                connection_factory.Ssl = new SslOption()
                                {
                                    Version = System.Security.Authentication.SslProtocols.Tls12,
                                    Enabled = true,
                                    AcceptablePolicyErrors = System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors | System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch
                                };
                            }
                        }
                        catch (Exception err)
                        {
                            return false;
                        }
                    }



                    if (connection == null)
                    {
                        try
                        {
                            connection = connection_factory.CreateConnection();

                            if (connection != null)
                            {
                                connection.ConnectionShutdown += (s, e) =>
                                {
                                    DebugText?.Invoke("RabbitMQ Connection Shut Down -> " + e.ReplyText);
                                };

                                connection.RecoverySucceeded += (s, e) =>
                                {
                                    DebugText?.Invoke("RabbitMQ Connetion Successfully Recovered");
                                };

                                connection.ConnectionRecoveryError += (s, e) =>
                                {
                                    DebugText?.Invoke("RabbitMQ Connection Failed to Recover");
                                };
                            }


                            DebugText?.Invoke("RabbitMQ Connected to " + connection_factory.HostName + " on port " + connection_factory.Port);
                        }
                        catch (Exception err)
                        {
                            return false;
                        }
                    }


                }
                catch (Exception err)
                {
                    return false;
                }
            }

            return true;
        }

        private bool verify_publish_channel()
        {
            lock (connectionLock)
            {
                try
                {
                    if (!check_connection())
                        return false;

                    if (publish_channel != null && publish_channel.IsOpen)
                        return true;

                    publish_channel = connection.CreateModel();

                    if (publish_channel != null && publish_channel.IsOpen)
                        return true;

                    return false;
                }
                catch
                {
                    return false;
                }
            }
        }

        private bool verify_response_channel()
        {
            lock (connectionLock)
            {
                try
                {
                    if (!check_connection())
                        return false;

                    if (response_channel != null && response_channel.IsOpen)
                        return true;

                    response_channel = connection.CreateModel();
                    if (response_channel == null)
                        return false;

                    responseQueueName = response_channel.QueueDeclare("", false, true, true);
                    var messageConsumer = new EventingBasicConsumer(response_channel);

                    messageConsumer.Received += (s, e) =>
                    {
                        if(e.BasicProperties != null && e.BasicProperties.CorrelationId != null && rpc_queue.ContainsKey(e.BasicProperties.CorrelationId))
                        {
                            if(rpc_queue.TryGetValue(e.BasicProperties.CorrelationId, out var rpcMessage))
                            {
                                rpcMessage.ExecuteCallback(BodySerializer, e.Body);
                                rpc_queue.TryRemove(e.BasicProperties.CorrelationId, out var v);
                            }
                        }
                    };

                    response_channel.BasicConsume(
                        queue: responseQueueName,
                        autoAck: true,
                        consumer: messageConsumer
                        );

                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        public void AddSubscription(IRabbitMQSubscription subscription)
        {
            subscription.Subscribe(connection, BodySerializer, stopSubscriptionsTokenSource.Token);
        }

        public void AddListenerSubscription<T>(Action<ListenerSubscription<T>> subscription)
        {
            if (subscription == null)
                throw new ArgumentNullException();

            var sub = new ListenerSubscription<T>();
            subscription.Invoke(sub);
            AddSubscription(sub);
        }

        public void AddRpcSubscription<T>(Action<RPCSubscription<T>> subscription)
        {
            if (subscription == null)
                throw new ArgumentNullException();

            var sub = new RPCSubscription<T>();
            subscription.Invoke(sub);
            AddSubscription(sub);
        }

        public void Publish(string exchange, string routingKey, object content)
        {
            publish_queue.Add(new RabbitMQMessage()
            {
                Exchange = exchange,
                RoutingKey = routingKey,
                content = content
            });
        }

        public void Call<T>(string exchange, string routingKey, object content,int timeoutSeconds,Action<T> ResponseCallback, Action TimeoutCallback) 
        {
            var correlationId = Guid.NewGuid().ToString();
            var msg = new RabbitMqRpcMessage<T>()
            {
                Exchange = exchange,
                RoutingKey = routingKey,
                content = content,
                SentTime = DateTime.Now,
                CorrelationId = correlationId,
                ValidTo = DateTime.Now.AddSeconds(timeoutSeconds),
                ResponseCallback = ResponseCallback,
                TimeoutCallback = TimeoutCallback
            };

            rpc_queue.TryAdd(correlationId, msg);
            publish_queue.Add(msg);
        }

        public async Task<T> CallAsync<T>(string exchange, string routingKey, object content, int timeoutSeconds)
        {
            return await Task.Run(() =>
            {
                BlockingCollection<T> waitQueue = new BlockingCollection<T>();
                Call<T>(exchange, routingKey, content, timeoutSeconds,
                    (resp) =>
                    {
                        waitQueue.Add(resp);
                    },
                    () =>
                    {
                        waitQueue.Add(default(T));
                    });

                return waitQueue.Take(stoppingToken);
            },stoppingToken);
        }
    }
}
