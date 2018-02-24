# RabbitMQ-Send-Test

Preparation
 - install "dotnet add package RabbitMQ.Client"
Terms used:
- what is a "socket"? Is one endpoint of a two-way communication link between two programs running on the network. A socket is bound to a port number so that the TCP layer can identify the application that data is destined to be sent to.
- 

## //LD STEP001 (sendVersion1 and receiveVersion1 methods)
###Implementation of basic message broker
resource -> https://www.rabbitmq.com/tutorials/tutorial-one-dotnet.html

Implementation of two simple methods: "producer" that sends a single message, and a "consumer" running continuously to listen, that receives messages and prints them out.

## //LD STEP002 (sendVersion2 and receiveVersion2 methods)
###Implementation of a Work Queue that will be used to distribute time-consuming tasks among multiple workers.
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

###//LD STEP002A - setup "automatic acknowledgement mode"
Manual message acknowledgments are turned on by default. In previous examples we explicitly turned them off by setting the autoAck ("automatic acknowledgement mode") parameter to true. It's time to remove this flag and manually send a proper acknowledgment from the worker, once we're done with a task.

Forgotten acknowledgment (command to display missed messages acknowledged)
 - rabbitmqctl.bat list_queues name messages_ready messages_unacknowledged

###//LD STEP002B - message durability
We have learned how to make sure that even if the consumer dies, the task isn't lost. But our tasks will still be lost if RabbitMQ server stops.

When RabbitMQ quits or crashes it will forget the queues and messages unless you tell it not to. Two things are required to make sure that messages aren't lost: we need to mark both the queue and messages as durable.

First, we need to make sure that RabbitMQ will never lose our queue. In order to do so, we need to declare it as durable.

Note: Although this command is correct by itself, it won't work in our present setup. That's because we've already defined a queue called hello which is not durable.

###//LD STEP002C - message persistent
At this point we're sure that the task_queue queue won't be lost even if RabbitMQ restarts. Now we need to mark our messages as persistent - by setting IBasicProperties.SetPersistent to true.

Note: Marking messages as persistent doesn't fully guarantee that a message won't be lost. Although it tells RabbitMQ to save the message to disk, there is still a short time window when RabbitMQ has accepted a message and hasn't saved it yet

###//LD STEP002D - fair message dispatch
You might have noticed that the dispatching still doesn't work exactly as we want. For example in a situation with two workers, when all odd messages are heavy and even messages are light, one worker will be constantly busy and the other one will do hardly any work. Well, RabbitMQ doesn't know anything about that and will still dispatch messages evenly.

This happens because RabbitMQ just dispatches a message when the message enters the queue. It doesn't look at the number of unacknowledged messages for a consumer. It just blindly dispatches every n-th message to the n-th consumer.

**Solution**: in order to change this behavior we can use the basicQos method with the prefetchCount = 1 setting. This tells RabbitMQ not to give more than one message to a worker at a time. Or, in other words, don't dispatch a new message to a worker until it has processed and acknowledged the previous one. Instead, it will dispatch it to the next worker that is not still busy.
 
code -> channel.BasicQos(0, 1, false);

Using message acknowledgments and BasicQos you can set up a work queue. The durability options let the tasks survive even if RabbitMQ is restarted.

For more information on IModel methods and IBasicProperties:
 - http://www.rabbitmq.com/releases/rabbitmq-dotnet-client/v3.6.10/rabbitmq-dotnet-client-3.6.10-client-htmldoc/html/index.html

## //LD STEP003 (sendVersion3 and receiveVersion3 methods)
### Use of the pattern "publish/subscribe", deliver a message to multiple consumers, published log messages are going to be broadcast to all the receivers. 
resource -> https://www.rabbitmq.com/tutorials/tutorial-three-dotnet.html

Description: to illustrate the pattern, we're going to build a simple logging system. It will consist of two programs, the first will emit log messages and the second will receive and print them.
In our logging system every running copy of the receiver program will get the messages. That way we'll be able to run one receiver and direct the logs to disk; and at the same time we'll be able to run another receiver and see the logs on the screen.