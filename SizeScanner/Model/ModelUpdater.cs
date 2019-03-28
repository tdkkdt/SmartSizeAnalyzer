using System;
using System.Collections.Concurrent;
using System.Threading;

namespace SizeScanner.Model {
    interface IUpdateTaskVisitor {
        void Visit(RenamedTask renamedTask);
        void Visit(DeletedTask deletedTask);
        void Visit(CreatedTask createdTask);
        void Visit(ChangedTask changedTask);
    }

    interface IUpdateTask {
        void Visit(IUpdateTaskVisitor visitor);
    }

    class RenamedTask : IUpdateTask {
        public string OldFullName { get; private set; }
        public string FullName { get; private set; }
        public string Name { get; private set; }
        public string OldName { get; private set; }

        public RenamedTask(string oldFullName, string fullName, string name, string oldName) {
            OldFullName = oldFullName;
            FullName = fullName;
            Name = name;
            OldName = oldName;
        }

        public void Visit(IUpdateTaskVisitor visitor) {
            visitor.Visit(this);
        }
    }

    class DeletedTask : IUpdateTask {
        public string Name { get; private set; }
        public string FullPath { get; private set; }

        public DeletedTask(string fullPath, string name) {
            FullPath = fullPath;
            Name = name;
        }

        public void Visit(IUpdateTaskVisitor visitor) {
            visitor.Visit(this);
        }
    }

    class CreatedTask : IUpdateTask {
        public string Name { get; private set; }
        public string FullPath { get; private set; }

        public CreatedTask(string fullPath, string name) {
            FullPath = fullPath;
            Name = name;
        }

        public void Visit(IUpdateTaskVisitor visitor) {
            visitor.Visit(this);
        }
    }

    class ChangedTask : IUpdateTask {
        public string Name { get; private set; }
        public string FullPath { get; private set; }

        public ChangedTask(string fullPath, string name) {
            FullPath = fullPath;
            Name = name;
        }

        public void Visit(IUpdateTaskVisitor visitor) {
            visitor.Visit(this);
        }
    }

    class ModelUpdater: IDisposable, IUpdateTaskVisitor {
        DirectoryModel DirectoryModel { get; set; }

        ConcurrentQueue<IUpdateTask> Tasks { get; set; }

        Thread UpdateThread { get; }

        ManualResetEventSlim ResetEvent { get; set; }

        public ModelUpdater(DirectoryModel model) {
            DirectoryModel = model;
            Tasks = new ConcurrentQueue<IUpdateTask>();
            UpdateThread = new Thread(DoTasks);
            UpdateThread.IsBackground = true;
            UpdateThread.Start();
            ResetEvent = new ManualResetEventSlim();
        }

        void DoTasks() {
            ResetEvent.Wait();
            while (true) {
                if (!Tasks.TryDequeue(out IUpdateTask task)) {
                    ResetEvent.Reset();
                    continue;
                }
                task.Visit(this);
            }
        }

        public void EnqueueTask(IUpdateTask task) {
            Tasks.Enqueue(task);
            ResetEvent.Set();
        }

        public void Dispose() {
            DirectoryModel = null;
            UpdateThread.Abort();
        }

        void IUpdateTaskVisitor.Visit(RenamedTask renamedTask) {
            DirectoryModel?.UpdateRename(renamedTask);
        }

        void IUpdateTaskVisitor.Visit(DeletedTask deletedTask) {
            DirectoryModel?.UpdateDelete(deletedTask);
        }

        void IUpdateTaskVisitor.Visit(CreatedTask createdTask) {
            DirectoryModel?.UpdateCreated(createdTask);
        }

        void IUpdateTaskVisitor.Visit(ChangedTask changedTask) {
            DirectoryModel?.UpdateSizeChanged(changedTask);
        }
    }
}
