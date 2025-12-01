SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

create procedure [dbo].[usp_enqueueHeap]
  @payload varbinary(max)
as
  set nocount on;
  insert into dbo.HeapQueue (Payload) values (@Payload);
GO


SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
create procedure [dbo].[usp_enqueuePending]
  @dueTime datetime,
  @payload varbinary(max)
as
  set nocount on;
  insert into dbo.PendingQueue (DueTime, Payload)
    values (@dueTime, @payload);
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

create procedure [dbo].[usp_enqueueFifo]
  @payload varbinary(max)
as
  set nocount on;
  insert into dbo.FifoQueue (Payload) values (@Payload);
GO


SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
create procedure [dbo].[usp_dequeuePending]
as
/*
Pending Queues
Another category of queues are pending queues. 
Items are inserted with a due date, and the dequeue operation returns rows that are due at dequeue time. 
This type of queues is common in scheduling systems.

I choose to use UTC times for my example, and I highly recommend you do the same for your applications. Not only this eliminates the problem of having to deal with timezones, but also your pending operations will behave correctly on the two times each year when summer time enters into effect or when it ends.

*/
  set nocount on;
  declare @now datetime;
  set @now = getutcdate();
  with cte as (
    select top(1) 
      Payload
    from dbo.PendingQueue with (rowlock, readpast)
    where DueTime < @now
    order by DueTime)
  delete from cte
    output deleted.Payload;
GO


SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
create procedure [dbo].[usp_dequeueHeap] 
as
/*
Heap Queues
The simplest queue is a heap: producers can equeue into the heap and consumers can dequeue, but order of operations is not important: the consumers can dequeue any row, as long as it is unlocked.

create table HeapQueue (
  Payload varbinary(max));
go

create procedure usp_enqueueHeap
  @payload varbinary(max)
as
  set nocount on;
  insert into HeapQueue (Payload) values (@Payload);
go

create procedure usp_dequeueHeap 
as
  set nocount on;
  delete top(1) from HeapQueue with (rowlock, readpast)
      output deleted.payload;      
go

A heap queue can satisfy most producer-consumer patterns. 
It scales well and is very simple to implement and understand. 
Notice the (rowlock, readpast) hints on the delete operation, they allow for concurrent consumers to dequeue rows 
from the table without blocking each other. 

A heap queue makes no order guarantees.
*/

  set nocount on;
  delete top(1) from dbo.HeapQueue with (rowlock, readpast)
      output deleted.payload;      
GO


SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE procedure [dbo].[usp_dequeueFifo]
as
/*
FIFO Queues
When he queueing and dequeuing operations have to support a certain order two changes have to be made:
The table must be organized as a clustered index ordered by a key that preserves the desired dequeue order.
The dequeue operation must contain an ORDER BY clause to guarantee the order.

By adding the IDENTITY column to our queue and making it the clustered index, we can dequeue in the order inserted. The enqueue operation is identical with our Heap Queue, but the dequeue is slightly changed, as the requirement to dequeue in the order inserted means that we have to specify an ORDER BY. Since the DELETE statement does not support ORDER BY, we use a Common Table Expression to select the row to be dequeued, then delete this row and return the payload in the OUTPUT clause. Isn’t this the same as doing a SELECT followed by a DELETE, and hence exposed to the traditional correctness problems with table backed queues? Technically, it is. But this is a SELECT followed by a DELETE that actually works for table based queues. Let me explain.
Because the query is actually an DELETE of a CTE, the query execution will occur as a DELETE, not as an SELECT followed by a DELETE, and also not as a SELECT executed in the context of the DELETE. The crucial part is that the SELECT part will aquire LCK_M_U update locks on the rows scanned. LCK_M_U is compatible with LCK_M_S shared locks, but is incompatible with another LCK_M_U. So two concurrent dequeue threads will not try to dequeue the same row. One will grab the first row free, the other thread will grab the next row.

*/
  set nocount on;
  with cte as 
  (
    select top(1) Payload
    from dbo.FifoQueue with (rowlock, readpast)
    order by Id
  )
  delete from cte
  output deleted.Payload;
GO


SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
create PROC [dbo].[get_work_queue_item]
AS
set NOCOUNT on;

UPDATE TOP(1)
 work_queue WITH (READPAST)
SET
 processed_flag = 1
OUTPUT
 inserted.work_queue_id,inserted.name, inserted.processed_flag
WHERE
 processed_flag = 0
GO


SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[PendingQueue](
	[DueTime] [datetime] NOT NULL,
	[Payload] [varbinary](max) NULL
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
CREATE CLUSTERED INDEX [cdxPendingQueue] ON [dbo].[PendingQueue]
(
	[DueTime] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO


SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[HeapQueue](
	[Payload] [varbinary](max) NULL
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[FifoQueue](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[Payload] [varbinary](max) NULL
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
CREATE CLUSTERED INDEX [cdxFifoQueue] ON [dbo].[FifoQueue]
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO


SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[work_queue](
	[work_queue_id] [int] IDENTITY(1,1) NOT NULL,
	[name] [varchar](255) NOT NULL,
	[processed_flag] [bit] NOT NULL
) ON [PRIMARY]
GO
ALTER TABLE [dbo].[work_queue] ADD  CONSTRAINT [PK_work_queue] PRIMARY KEY CLUSTERED 
(
	[work_queue_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO

