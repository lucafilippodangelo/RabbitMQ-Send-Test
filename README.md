# RabbitMQ Send Test

Preparation
 - install "dotnet add package RabbitMQ.Client"

Terms used:
- what is a "socket"? Is one endpoint of a two-way communication link between two programs running on the network. A socket is bound to a port number so that the TCP layer can identify the application that data is destined to be sent to.

## //LD STEP001 (sendVersion1 and receiveVersion1 methods)
### Implementation of basic message broker
resource -> https://www.rabbitmq.com/tutorials/tutorial-one-dotnet.html

Implementation of two simple methods: "producer" that sends a single message, and a "consumer" running continuously to listen, that receives messages and prints them out.

## //LD STEP002 (sendVersion2 and receiveVersion2 methods)
### Implementation of a Work Queue that will be used to distribute time-consuming tasks among multiple workers.
resource -> https://www.rabbitmq.com/tutorials/tutorial-two-dotnet.html


The main idea behind Work Queues (aka: Task Queues) is to avoid doing a resource-intensive task immediately and having to wait for it to complete. Instead we schedule the task to be done later. 

GOAL: encapsulate a task as a message and send it to a queue. A worker process running in the background will pop the tasks and eventually execute the job. When you run many workers the tasks will be shared between them.

One of the advantages of using a Task Queue is the ability to easily parallelise work. If we are building up a backlog of work, we can just add more workers and that way, scale easily.

Run The Test:
- send
  - run in console one instance of "sendVersion2"
    - go in the folder where is the "*.csproj" file of the console application -> dotnet run 
      - the method will send an array of 10 strings that need to be processed by the worker.
- receive
  - run in two different console two instances of "receiveVersion2"
    - By default, RabbitMQ will send each message to the next consumer, in sequence. On average every consumer will get the same number of messages. This way of distributing messages is called **round-robin**.

Reflection:
Doing a task can take a few seconds. You may wonder what happens if one of the consumers starts a long task and dies with it only partly done. With our current code, once RabbitMQ delivers a message to the customer it immediately marks it for deletion. In this case, if you kill a worker we will lose the message it was just processing. We'll also lose all the messages that were dispatched to this particular worker but were not yet handled.

**But we don't want to lose any tasks. If a worker dies, we'd like the task to be delivered to another worker.**

In order to make sure a message is never lost, RabbitMQ supports **message acknowledgments**. An ack(nowledgement) is sent back by the consumer to tell RabbitMQ that a particular message has been received, processed and that RabbitMQ is free to delete it.

If a consumer dies (its channel is closed, connection is closed, or TCP connection is lost) without sending an ack, RabbitMQ will understand that a message wasn't processed fully and will re-queue it. If there are other consumers online at the same time, it will then quickly redeliver it to another consumer. That way you can be sure that no message is lost, even if the workers occasionally die.

There aren't any message timeouts; RabbitMQ will redeliver the message when the consumer dies. It's fine even if processing a message takes a very, very long time.

### //LD STEP002A - setup "automatic acknowledgement mode"
Manual message acknowledgments are turned on by default. In previous examples we explicitly turned them off by setting the autoAck ("automatic acknowledgement mode") parameter to true. It's time to remove this flag and manually send a proper acknowledgment from the worker, once we're done with a task.

Forgotten acknowledgment (command to display missed messages acknowledged)
 - rabbitmqctl.bat list_queues name messages_ready messages_unacknowledged

### //LD STEP002B - message durability
We have learned how to make sure that even if the consumer dies, the task isn't lost. But our tasks will still be lost if RabbitMQ server stops.

When RabbitMQ quits or crashes it will forget the queues and messages unless you tell it not to. Two things are required to make sure that messages aren't lost: we need to mark both the queue and messages as durable.

First, we need to make sure that RabbitMQ will never lose our queue. In order to do so, we need to declare it as durable.

Note: Although this command is correct by itself, it won't work in our present setup. That's because we've already defined a queue called hello which is not durable.

### //LD STEP002C - message persistent
At this point we're sure that the task_queue queue won't be lost even if RabbitMQ restarts. Now we need to mark our messages as persistent - by setting IBasicProperties.SetPersistent to true.

Note: Marking messages as persistent doesn't fully guarantee that a message won't be lost. Although it tells RabbitMQ to save the message to disk, there is still a short time window when RabbitMQ has accepted a message and hasn't saved it yet

### //LD STEP002D - fair message dispatch
You might have noticed that the dispatching still doesn't work exactly as we want. For example in a situation with two workers, when all odd messages are heavy and even messages are light, one worker will be constantly busy and the other one will do hardly any work. Well, RabbitMQ doesn't know anything about that and will still dispatch messages evenly.

This happens because RabbitMQ just dispatches a message when the message enters the queue. It doesn't look at the number of unacknowledged messages for a consumer. It just blindly dispatches every n-th message to the n-th consumer.

**Solution**: in order to change this behavior we can use the basicQos method with the prefetchCount = 1 setting. This tells RabbitMQ not to give more than one message to a worker at a time. Or, in other words, don't dispatch a new message to a worker until it has processed and acknowledged the previous one. Instead, it will dispatch it to the next worker that is not still busy.
 
code -> channel.BasicQos(0, 1, false);

Using message acknowledgments and BasicQos you can set up a work queue. The durability options let the tasks survive even if RabbitMQ is restarted.

For more information on IModel methods and IBasicProperties:
 - http://www.rabbitmq.com/releases/rabbitmq-dotnet-client/v3.6.10/rabbitmq-dotnet-client-3.6.10-client-htmldoc/html/index.html

## //LD STEP003 (sendVersion3 and receiveVersion3 methods)
### Use of the pattern "publish/subscribe", implementation of a simple logging system. Will be able to broadcast log messages to many receivers.
resource -> https://www.rabbitmq.com/tutorials/tutorial-three-dotnet.html

**Description**: to illustrate the pattern, we're going to build a simple logging system. It will consist of two programs, the first will emit log messages and the second will receive and print them.
In our logging system every running copy of the receiver program will get the messages. That way we'll be able to run one receiver and direct the logs to disk; and at the same time we'll be able to run another receiver and see the logs on the screen.

**Exchange concept**: the producer can only send messages to an "exchange". An exchange on one side it receives messages from producers and the other side it pushes them to queues. The exchange must know exactly what to do with a message it receives. Should it be appended to a particular queue? Should it be appended to many queues? Or should it get discarded. The rules for that are defined by the exchange type.

There are a few exchange types available: 
 - direct
 - topic
 - headers 
 - fanout
   - //LD STEP003B, code -> channel.ExchangeDeclare("logs", "fanout");
   - description: broadcasts all the messages it receives to all the queues it knows.

To list the exchanges on the server you can run -> sudo rabbitmqctl list_exchanges

### //LD STEP003A - //LD STEP003B - the default exchange (sender)

The first parameter is the the name of the exchange. The empty string denotes the default or nameless exchange: messages are routed to the queue with the name specified by routingKey, if it exists.
Giving a queue a name is important when you want to share the queue between producers and consumers

Now, we can publish to our named exchange //LD STEP003B instead of the queue (step2 //LD STEP003A)
 - code -> channel.BasicPublish(exchange: "logs", routingKey: "", basicProperties: null, body: body);

**Note**: The messages will be lost if no queue is bound to the exchange yet, but that's okay for us; if no consumer is listening yet we can safely discard the message.

### //LD STEP003C - Temporary queues 

Previously we were using queues which had a specified name (remember hello and task_queue?). Being able to name a queue was crucial for us -- we needed to point the workers to the same queue. Giving a queue a name is important when you want to share the queue between producers and consumers.

For the logger, We want to hear about all log messages, not just a subset of them. We're also interested only in currently flowing messages not in the old ones. To solve that we need two things:
 - //LD STEP003C, Firstly, whenever we connect to Rabbit we need a fresh, empty queue. To do this we could create a queue with a random name, or, even better - let the server choose a random queue name for us. 
 - Secondly, once we disconnect the consumer the queue should be automatically deleted.

In the .NET client, when we supply no parameters to queueDeclare() we create a non-durable, exclusive, autodelete queue with a generated name:
 - code -> var queueName = channel.QueueDeclare().QueueName;
 - for more details: https://www.rabbitmq.com/queues.html

At that point queueName contains a random queue name. For example it may look like amq.gen-JzTY20BRgKO-HjmUJj0wLg.

### //LD STEP003D - Bindings

We've already created a fanout exchange and a queue. Now we need to tell the exchange to send messages to our queue. That relationship between exchange and a queue is called a binding.

From now on the logs exchange will append messages to our queue.

You can list existing bindings using -> rabbitmqctl list_bindings

### How to test
- run two or more instances of "receiveVersion3" in "windows powershell"
  - cd C:\Users\ldazu\source\repos\Git\RabbitMQ-Receive-Test\receive -> dotnet run
- run one instance of "receiveVersion3" in Visual studio or consolle
- both the receivers should receive the same logs, and then use those as preferred.

## //LD STEP004 (sendVersion4 and receiveVersion4 methods)
### Implementation of improvements on the logging system. Instead of using a fanout exchange only capable of dummy broadcasting, here is used a direct one, gained a possibility of selectively receiving the logs.
resource -> https://www.rabbitmq.com/tutorials/tutorial-four-dotnet.html

### //LD STEP004A - binding key and subscribing (receiver)
Bindings can take an extra "routingKey" parameter. To avoid the confusion with a "BasicPublish" parameter we're going to call it a "binding key".

### //LD STEP004B - Direct exchange

Our logging system from the previous tutorial broadcasts all messages to all consumers. We want to extend that to allow filtering messages based on their severity.

We were using a **fanout** exchange, which doesn't give us much flexibility - it's only capable of mindless broadcasting.

We will use a **direct exchange** instead. The routing algorithm behind a direct exchange is simple: a message goes to the queues whose binding key exactly matches the routing key of the message.

Note: see code demo

**Multiple bindings**(see tutoria for illustrated demo): It is perfectly legal to bind multiple queues with the same binding key. In our example we could add a binding between X and Q1 with binding key black. In that case, the direct exchange will behave like fanout and will broadcast the message to all the matching queues. A message with routing key black will be delivered to both Q1 and Q2. 

### //LD STEP004C - Emitting logs, "direct" Exchange creation (sender)

### //LD STEP004D - Emitting logs, send the message (sender)

We will supply the log "severity" as a routing key. That way the receiving script will be able to select the severity it wants to receive. To simplify things we will assume that 'severity' can be one of 'info', 'warning', 'error'.

### How to test
just run "sendVersion4" and "receiveVersion4", once I'm binding in the receiver just the routingKey: 'orange' for a specific queue, below the expected result:

- Sender
 ```
 [x] Sent 'green':'aaa-green'
 [x] Sent 'orange':'bbb-orange'
 [x] Sent 'red':'ccc-red'
 [x] Sent 'red':'ddd-red'
 [x] Sent 'red':'eee-red'
 [x] Sent 'orange':'fff-orange'
 [x] Sent 'green':'ggg-green.'
 [x] Sent 'orange':'hhh-orange'
 [x] Sent 'green':'iii-green'
 task_queue - Press [enter] to exit.
 ```

- Receiver
 
 ```
 PS C:\Users\ldazu> cd C:\Users\ldazu\source\repos\Git\RabbitMQ-Receive-Test\receive
 PS C:\Users\ldazu\source\repos\Git\RabbitMQ-Receive-Test\receive> dotnet run
 [*] Waiting for logs
 Press [enter] to exit.
 [x] Received 'orange':'bbb-orange'
 [x] Received 'orange':'fff-orange'
 [x] Received 'orange':'hhh-orange'
 ```
 

## //LD STEP005 (sendVersion5 and receiveVersion5 methods)
### Use of Topic Exchange. Implementation of updates to the code in order to subscribe to not only logs based on severity, but also based on the source which emitted the log.
https://www.rabbitmq.com/tutorials/tutorial-five-dotnet.html

### //LD STEP005A - Topic exchange

Messages sent to a **topic** exchange can't have an arbitrary **routing_key** - it must be a list of words, delimited by dots. The words can be anything, but usually they specify some features connected to the message. A few valid routing key examples: "stock.usd.nyse", "nyse.vmw", "quick.orange.rabbit". There can be as many words in the routing key as you like, up to the limit of 255 bytes.

The binding key must also be in the same form. The logic behind the **topic exchange** is similar to a **direct** one - a message sent with a particular routing key will be delivered to all the queues that are bound with a matching binding key. However there are two important special cases for binding keys:
 - "*" (star) can substitute for exactly one word.
 - "#" (hash) can substitute for zero or more words.

### //LD STEP005B - Topic exchange example to implement

We're going to use a topic exchange in our logging system. We'll start off with a working assumption that the routing keys of logs will have two words: "<facility>.<severity>".

### How to test
just run "sendVersion5" and "receiveVersion5", once I'm binding in the receiver just the routingKey: 'orange' for a specific queue, below the expected result:

- Sender
 ```
 [x] Sent 'facilityA.green':'aaa-green'
 [x] Sent 'facilityB.orange':'bbb-orange'
 [x] Sent 'facilityC.red':'ccc-red'
 [x] Sent 'facilityD.red':'ddd-red'
 [x] Sent 'facilityE.red':'eee-red'
 [x] Sent 'facilityF.orange':'fff-orange'
 [x] Sent 'facilityG.green':'ggg-green.'
 [x] Sent 'facilityH.orange':'hhh-orange'
 [x] Sent 'facilityI.green':'iii-green'
 task_queue - Press [enter] to exit.
 ```

- Receiver
 ```
PS C:\Users\ldazu> cd C:\Users\ldazu\source\repos\Git\RabbitMQ-Receive-Test\receive
PS C:\Users\ldazu\source\repos\Git\RabbitMQ-Receive-Test\receive> dotnet run
 [*] Waiting for logs
 Press [enter] to exit.
 [x] Received 'facilityB.orange':'bbb-orange'
 [x] Received 'facilityF.orange':'fff-orange'
 [x] Received 'facilityH.orange':'hhh-orange'
 ```


 Resources:
 - https://www.compose.com/articles/configuring-rabbitmq-exchanges-queues-and-bindings-part-2/
 - https://www.cloudamqp.com/blog/2015-09-03-part4-rabbitmq-for-beginners-exchanges-routing-keys-bindings.html
 - https://docs.wso2.com/display/MB220/Publishing+and+Receiving+Messages+from+a+Topic