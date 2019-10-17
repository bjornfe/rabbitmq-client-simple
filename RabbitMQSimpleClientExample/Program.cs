using RabbitMQ.Client.Simple;
using System;

namespace RabbitMQSimpleClientExample
{
    class Program
    {
        public class ExampleEvent
        {
            public string EventName { get; set; }
        }

        public class CommandResult
        {
            public int StatusCode { get; set; }
            public string Message { get; set; }
        }

        public class ExampleCommand
        {
            public string CommandText { get; set; }
        }

        static void Main(string[] args)
        {
            var rmqService = new RabbitMQServiceBuilder()
                .UseJsonSerializer()
                .SetDebugWriter(txt=> Console.WriteLine($"[ {DateTime.Now.ToString("HH:mm:ss.fff")} ] {txt}"))
                .SetOptions(opt =>
                {
                    opt.Hostname = "<hostname>";
                    opt.Port = 5671;
                    opt.UseSSL = true;
                    opt.Username = "<username>";
                    opt.Password = "<password>";
                    opt.VirtualHost = "/";

                })
                .AddListenerSubscription<ExampleEvent>(opt =>
                {
                    opt.QueueName = "";
                    opt.Exchange = "amq.topic";
                    opt.Exclusive = true;
                    opt.AutoAck = true;
                    opt.AutoDelete = true;
                    opt.Durable = false;
                    opt.RoutingKey = "event.example";
                    opt.DebugText += (txt) =>
                    {
                        Console.WriteLine(txt);
                    };
                    opt.Callback += (evt) =>
                    {
                        Console.WriteLine($"[ {DateTime.Now.ToString("HH:mm:ss.fff")} ] Received Event with name: {evt.EventName}");
                    };
                })
                .AddRpcSubscription<ExampleCommand>(opt =>
                {
                    opt.QueueName = typeof(ExampleCommand).Name;
                    opt.Exchange = "";
                    opt.Exclusive = false;
                    opt.AutoAck = false;
                    opt.AutoDelete = false;
                    opt.Durable = true;
                    opt.DebugText += (txt) =>
                    {
                        Console.WriteLine(txt);
                    };
                    opt.Callback += (cmd) =>
                    {
                        Console.WriteLine($"[ {DateTime.Now.ToString("HH:mm:ss.fff")} ] Handling command");
                        return new CommandResult()
                        {
                            StatusCode = 200,
                            Message = cmd.CommandText
                        };
                    };

                })
                .AddListenerSubscription(new ExampleSubscription())
                .Build();


            Console.WriteLine($"[ {DateTime.Now} ] Sending Event");
            rmqService.Publish("amq.topic", "event.example", new ExampleEvent()
            {
                EventName = "This is an example event"
            });

            var cmd = new ExampleCommand()
            {
                CommandText = "This is an example command"
            };

            Console.WriteLine($"[ {DateTime.Now} ] Sending Command");
            rmqService.Call<CommandResult>(
                exchange: "",
                routingKey: typeof(ExampleCommand).Name,
                content: cmd,
                timeoutSeconds: 10,
                ResponseCallback: (res) =>
                 {
                     Console.WriteLine($"[ {DateTime.Now.ToString("HH:mm:ss.fff")} ] Received Command Result with code: {res.StatusCode} and message: {res.Message}");
                 },
                TimeoutCallback: () =>
                 {
                     Console.WriteLine($"[ {DateTime.Now.ToString("HH:mm:ss.fff")} ] Command Timed out");
                 });


            //Example of targeting the "ExampleSubscription.cs" class.
            var cmd2 = new ExampleSubscriptionObject()
            {
                ObjectContent = "This is another example command"
            };

            Console.WriteLine($"[ {DateTime.Now} ] Sending Command 2");
            rmqService.Call<ExampleRpcResult>(
                exchange: "",
                routingKey: typeof(ExampleSubscriptionObject).Name,
                content: cmd2,
                timeoutSeconds: 10,
                ResponseCallback: (res) =>
                {
                    Console.WriteLine($"[ {DateTime.Now.ToString("HH:mm:ss.fff")} ] Received Command 2 Result with OK: {res.ExampleWasOk} and message: {res.ExampleResultMessage}");
                },
                TimeoutCallback: () =>
                {
                    Console.WriteLine($"[ {DateTime.Now.ToString("HH:mm:ss.fff")} ] Command Timed out");
                });

            Console.ReadLine();
        }
    }
}
