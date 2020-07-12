using System;
using Baseline;
using LamarCodeGeneration;
using Marten.Events;
using Marten.Schema;
using Marten.Util;

namespace Marten.V4Internals
{
    public class ProviderGraph: IProviderGraph
    {
        private readonly StoreOptions _options;
        private ImHashMap<Type, object> _storage = ImHashMap<Type, object>.Empty;

        public ProviderGraph(StoreOptions options)
        {
            _options = options;
        }

        public DocumentProvider<T> StorageFor<T>()
        {
            if (_storage.TryFind(typeof(T), out var stored))
            {
                return stored.As<DocumentProvider<T>>();
            }

            if (typeof(T) == typeof(IEvent))
            {
                var storage = new EventDocumentStorage(_options.Events, new EventQueryMapping(_options), _options.Serializer());
                var slot = new DocumentProvider<IEvent>
                {
                    DirtyTracking = storage,
                    Lightweight = storage,
                    IdentityMap = storage,
                    QueryOnly = storage
                };

                _storage = _storage.AddOrUpdate(typeof(T), slot);

                return slot.As<DocumentProvider<T>>();
            }

            var mapping = _options.Storage.FindMapping(typeof(T));

            if (mapping is DocumentMapping m)
            {
                var builder = new DocumentPersistenceBuilder(m, _options);
                var slot = builder.Generate<T>();

                _storage = _storage.AddOrUpdate(typeof(T), slot);

                return slot;
            }

            if (mapping is SubClassMapping s)
            {
                var loader =
                    typeof(SubClassLoader<,,>).CloseAndBuildAs<ISubClassLoader<T>>(mapping.Root.DocumentType, typeof(T),
                        mapping.IdType);

                var slot = loader.BuildPersistence(this, s);
                _storage = _storage.AddOrUpdate(typeof(T), slot);

                return slot;
            }

            if (mapping is EventMapping em)
            {
                var storage = (IDocumentStorage<T>) em;
                var slot = new DocumentProvider<T> {Lightweight = storage, IdentityMap = storage, DirtyTracking = storage, QueryOnly = storage};
                _storage = _storage.AddOrUpdate(typeof(T), slot);

                return slot;
            }

            throw new NotSupportedException("Unable to build document persistence handlers for " + mapping.DocumentType.FullNameInCode());

        }

        private interface ISubClassLoader<T>
        {
            DocumentProvider<T> BuildPersistence(IProviderGraph graph, SubClassMapping mapping);
        }

        private class SubClassLoader<TRoot, T, TId> : ISubClassLoader<T> where T : TRoot
        {
            public DocumentProvider<T> BuildPersistence(IProviderGraph graph, SubClassMapping mapping)
            {
                var inner = graph.StorageFor<TRoot>();

                return new DocumentProvider<T>()
                {
                    QueryOnly = new SubClassDocumentStorage<T, TRoot, TId>((IDocumentStorage<TRoot, TId>) inner.QueryOnly, mapping),
                    Lightweight = new SubClassDocumentStorage<T, TRoot, TId>((IDocumentStorage<TRoot, TId>) inner.Lightweight, mapping),
                    IdentityMap = new SubClassDocumentStorage<T, TRoot, TId>((IDocumentStorage<TRoot, TId>) inner.IdentityMap, mapping),
                    DirtyTracking = new SubClassDocumentStorage<T, TRoot, TId>((IDocumentStorage<TRoot, TId>) inner.DirtyTracking, mapping),
                    BulkLoader = new SubClassBulkLoader<T, TRoot>(inner.BulkLoader)
                };
            }
        }
    }
}
