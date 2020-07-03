using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Baseline;
using Marten.Events;
using Marten.Linq;
using Marten.Patching;
using Marten.Services;
using Marten.Storage;

namespace Marten.V4Internals.Sessions
{
    public abstract class NewDocumentSession: QuerySession, IDocumentSession
    {
        // The current unit of work can be replaced
        protected UnitOfWork _unitOfWork = new UnitOfWork();


        protected NewDocumentSession(DocumentStore store, SessionOptions sessionOptions, IManagedConnection database,
            ITenant tenant) : base(store, sessionOptions, database, tenant)
        {
            Concurrency = sessionOptions.ConcurrencyChecks;
        }


        public void Delete<T>(T entity)
        {
            assertNotDisposed();
            var deletion = storageFor<T>().DeleteForDocument(entity);
            _unitOfWork.Add(deletion);

            storageFor<T>().Eject(this, entity);
        }

        public void Delete<T>(int id)
        {
            assertNotDisposed();
            var deletion = storageFor<T, int>().DeleteForId(id);
            _unitOfWork.Add(deletion);

            ejectById<T>(id);
        }

        protected abstract void ejectById<T>(long id);
        protected abstract void ejectById<T>(int id);
        protected abstract void ejectById<T>(Guid id);
        protected abstract void ejectById<T>(string id);

        public void Delete<T>(long id)
        {
            assertNotDisposed();
            var deletion = storageFor<T, long>().DeleteForId(id);
            _unitOfWork.Add(deletion);

            ejectById<T>(id);
        }

        public void Delete<T>(Guid id)
        {
            assertNotDisposed();
            var deletion = storageFor<T, Guid>().DeleteForId(id);
            _unitOfWork.Add(deletion);

            ejectById<T>(id);
        }

        public void Delete<T>(string id)
        {
            assertNotDisposed();
            var deletion = storageFor<T, string>().DeleteForId(id);
            _unitOfWork.Add(deletion);

            ejectById<T>(id);
        }

        public void DeleteWhere<T>(Expression<Func<T, bool>> expression)
        {
            assertNotDisposed();

            // TODO -- memoize the parser
            var parser = new MartenExpressionParser(Options.Serializer(), Options);

            // TODO -- this could be cleaner maybe?
            var documentStorage = storageFor<T>();
            var @where = parser.ParseWhereFragment(documentStorage.Fields, expression);
            var deletion = documentStorage.DeleteForWhere(@where);
            _unitOfWork.Add(deletion);
        }

        public void SaveChanges()
        {
            assertNotDisposed();

            if (!_unitOfWork.HasOutstandingWork()) return;

            Database.BeginTransaction();

            // TODO -- apply inline projections

            _unitOfWork.Sort(Options);

            foreach (var listener in Listeners)
            {
                listener.BeforeSaveChanges(this);
            }

            var batch = new UpdateBatch(_unitOfWork.AllOperations);
            try
            {
                batch.ApplyChanges(this);
                Database.Commit();
            }
            catch (Exception)
            {
                Database.Rollback();
                throw;
            }

            clearDirtyChecking();

            EjectPatchedTypes(_unitOfWork);
            Logger.RecordSavedChanges(this, _unitOfWork);

            foreach (var listener in Listeners)
            {
                listener.AfterCommit(this, _unitOfWork);
            }

            // Need to clear the unit of work here
            _unitOfWork = new UnitOfWork();
        }

        protected virtual void clearDirtyChecking()
        {
            // Nothing
        }

        public async Task SaveChangesAsync(CancellationToken token = default)
        {
            assertNotDisposed();

            if (!_unitOfWork.HasOutstandingWork()) return;

            await Database.BeginTransactionAsync(token).ConfigureAwait(false);

            // TODO -- apply inline projections

            _unitOfWork.Sort(Options);

            foreach (var listener in Listeners)
            {
                await listener.BeforeSaveChangesAsync(this, token).ConfigureAwait(false);
            }

            var batch = new UpdateBatch(_unitOfWork.AllOperations);
            try
            {
                await batch.ApplyChangesAsync(this, token).ConfigureAwait(false);
                await Database.CommitAsync(token).ConfigureAwait(false);
            }
            catch (Exception)
            {
                await Database.RollbackAsync(token).ConfigureAwait(false);
                throw;
            }

            clearDirtyChecking();

            EjectPatchedTypes(_unitOfWork);
            Logger.RecordSavedChanges(this, _unitOfWork);

            foreach (var listener in Listeners)
            {
                await listener.AfterCommitAsync(this, _unitOfWork, token).ConfigureAwait(false);
            }

            // Need to clear the unit of work here
            _unitOfWork = new UnitOfWork();
        }

        public void Store<T>(IEnumerable<T> entities)
        {
            Store(entities?.ToArray());
        }

        public void Store<T>(params T[] entities)
        {
            if (entities == null)
                throw new ArgumentNullException(nameof(entities));

            if (typeof(T).IsGenericEnumerable())
                throw new ArgumentOutOfRangeException(typeof(T).Name, "Do not use IEnumerable<T> here as the document type. Either cast entities to an array instead or use the IEnumerable<T> Store() overload instead.");

            store(entities);
        }


        private void store<T>(IEnumerable<T> entities)
        {
            assertNotDisposed();

            if (typeof(T) == typeof(object))
            {
                StoreObjects(entities.OfType<object>());
            }
            else
            {
                var storage = storageFor<T>();

                foreach (var entity in entities)
                {
                    var upsert = storage.Upsert(entity, this, Tenant);

                    // Put it in the identity map -- if necessary
                    storage.Store(this, entity);

                    _unitOfWork.Add(upsert);


                }
            }
        }


        public void Store<T>(string tenantId, IEnumerable<T> entities)
        {
            Store(tenantId, entities?.ToArray());
        }

        public void Store<T>(string tenantId, params T[] entities)
        {
            assertNotDisposed();

            if (entities == null)
            {
                throw new ArgumentNullException(nameof(entities));
            }

            if (typeof(T).IsGenericEnumerable())
            {
                throw new ArgumentOutOfRangeException(typeof(T).Name, "Do not use IEnumerable<T> here as the document type. Cast entities to an array or use the IEnumerable<T> Store() overload instead.");
            }

            var tenant = DocumentStore.Tenancy[tenantId];

            var storage = tenant.StorageFor<T>();

            foreach (var entity in entities)
            {
                var op = storage.Upsert(entity, this, tenant);
                storage.Store(this, entity);

                _unitOfWork.Add(op);
            }
        }

        public void Store<T>(T entity, Guid version)
        {
            assertNotDisposed();

            var storage = storageFor<T>();
            storage.Store(this, entity, version);
            var op = storage.Upsert(entity, this, Tenant);
            _unitOfWork.Add(op);
        }

        public void Insert<T>(IEnumerable<T> entities)
        {
            Insert(entities.ToArray());
        }

        public void Insert<T>(params T[] entities)
        {
            assertNotDisposed();

            if (entities == null)
            {
                throw new ArgumentNullException(nameof(entities));
            }

            if (typeof(T).IsGenericEnumerable())
            {
                throw new ArgumentOutOfRangeException(typeof(T).Name, "Do not use IEnumerable<T> here as the document type. You may need to cast entities to an array instead.");
            }

            if (typeof(T) == typeof(object))
            {
                InsertObjects(entities.OfType<object>());
            }
            else
            {
                var storage = storageFor<T>();

                foreach (var entity in entities)
                {
                    storage.Store(this, entity);
                    var op = storage.Insert(entity, this, Tenant);
                    _unitOfWork.Add(op);
                }
            }
        }

        public void Update<T>(IEnumerable<T> entities)
        {
            Update(entities.ToArray());
        }

        public void Update<T>(params T[] entities)
        {
            assertNotDisposed();

            if (entities == null)
            {
                throw new ArgumentNullException(nameof(entities));
            }

            if (typeof(T).IsGenericEnumerable())
            {
                throw new ArgumentOutOfRangeException(typeof(T).Name, "Do not use IEnumerable<T> here as the document type. You may need to cast entities to an array instead.");
            }

            if (typeof(T) == typeof(object))
            {
                InsertObjects(entities.OfType<object>());
            }
            else
            {
                var storage = storageFor<T>();

                foreach (var entity in entities)
                {
                    storage.Store(this, entity);
                    var op = storage.Update(entity, this, Tenant);
                    _unitOfWork.Add(op);
                }
            }
        }

        public void InsertObjects(IEnumerable<object> documents)
        {
            assertNotDisposed();

            documents.Where(x => x != null).GroupBy(x => x.GetType()).Each(group =>
            {
                var handler = typeof(InsertHandler<>).CloseAndBuildAs<IHandler>(group.Key);
                handler.Store(this, group);
            });
        }

        public IUnitOfWork PendingChanges => _unitOfWork;

        public void StoreObjects(IEnumerable<object> documents)
        {
            assertNotDisposed();

            documents.Where(x => x != null).GroupBy(x => x.GetType()).Each(group =>
            {
                var handler = typeof(Handler<>).CloseAndBuildAs<IHandler>(group.Key);
                handler.Store(this, group);
            });
        }

        internal interface IHandler
        {
            void Store(IDocumentSession session, IEnumerable<object> objects);
        }

        internal class Handler<T>: IHandler
        {
            public void Store(IDocumentSession session, IEnumerable<object> objects)
            {
                session.Store(objects.OfType<T>().ToArray());
            }
        }

        internal class InsertHandler<T>: IHandler
        {
            public void Store(IDocumentSession session, IEnumerable<object> objects)
            {
                session.Insert(objects.OfType<T>().ToArray());
            }
        }

        public IEventStore Events { get; }
        public ConcurrencyChecks Concurrency { get; set; } = ConcurrencyChecks.Enabled;

        public IPatchExpression<T> Patch<T>(int id)
        {
            return patchById<T>(id);
        }

        public IPatchExpression<T> Patch<T>(long id)
        {
            return patchById<T>(id);
        }

        public IPatchExpression<T> Patch<T>(string id)
        {
            return patchById<T>(id);
        }

        public IPatchExpression<T> Patch<T>(Guid id)
        {
            return patchById<T>(id);
        }

        public IPatchExpression<T> Patch<T>(Expression<Func<T, bool>> filter)
        {
            assertNotDisposed();

            var queryable = Query<T>().Where(filter);
            var model = MartenQueryParser.Flyweight.GetParsedQuery(queryable.Expression);

            var storage = storageFor<T>();

            // TODO -- parser needs to be a singleton in the system
            var @where = storage.BuildWhereFragment(model, new MartenExpressionParser(Serializer, Options));

            return new PatchExpression<T>(@where, this);
        }

        public IPatchExpression<T> Patch<T>(IWhereFragment fragment)
        {
            assertNotDisposed();

            return new PatchExpression<T>(fragment, this);
        }

        private IPatchExpression<T> patchById<T>(object id)
        {
            assertNotDisposed();

            var @where = new WhereFragment("d.id = ?", id);
            return new PatchExpression<T>(@where, this);
        }


        public void QueueOperation(IStorageOperation storageOperation)
        {
            _unitOfWork.Add(storageOperation);
        }

        public virtual void Eject<T>(T document)
        {
            storageFor<T>().Eject(this, document);
            _unitOfWork.Eject(document);
        }

        public virtual void EjectAllOfType(Type type)
        {
            ItemMap.Remove(type);
        }

        public void EjectPatchedTypes(IUnitOfWork changes)
        {
            var patchedTypes = changes.Patches().Select(x => x.DocumentType).Distinct().ToArray();
            foreach (var type in patchedTypes)
            {
                EjectAllOfType(type);
            }
        }


    }
}
