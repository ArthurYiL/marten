using System;
using System.Collections.Generic;
using LamarCodeGeneration;

namespace Marten.V4Internals.Linq.Includes
{
    public static class Include
    {
        public static IIncludeReader ReaderToAction<T>(IMartenSession session, Action<T> action)
        {
            var storage = session.StorageFor<T>();

            var selector = (ISelector<T>) storage.BuildSelector(session);
            return new IncludeReader<T>(action, selector);
        }

        public static IIncludeReader ReaderToList<T>(IMartenSession session, IList<T> list)
        {
            return ReaderToAction<T>(session, list.Add);
        }

        public static IIncludeReader ReaderToDictionary<T, TId>(IMartenSession session, IDictionary<TId, T> dictionary)
        {
            var storage = session.StorageFor<T>();
            if (storage is IDocumentStorage<T, TId> s)
            {
                void Callback(T item)
                {
                    var id = s.Identity(item);
                    dictionary[id] = item;
                }

                var selector = (ISelector<T>) storage.BuildSelector(session);
                return new IncludeReader<T>(Callback, selector);
            }
            else
            {
                throw new InvalidOperationException($"Document type {typeof(T).FullNameInCode()} has an id type of {storage.IdType.NameInCode()}, but was used with {typeof(TId).NameInCode()}");
            }
        }
    }
}
