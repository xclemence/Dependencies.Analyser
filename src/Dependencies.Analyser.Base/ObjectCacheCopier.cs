using System;
using System.Collections.Generic;

namespace Dependencies.Analyser.Base
{
    public class ObjectCacheTransformer
    {
        private readonly IDictionary<Type, IDictionary<dynamic, dynamic>> caches;

        public ObjectCacheTransformer()
        {
            caches = new Dictionary<Type, IDictionary<dynamic, dynamic>>();
        }

        public To Transform<From, To>(From obj, Func<From, To> transform)
        {
            var type = typeof(From);

            if (!caches.TryGetValue(type, out IDictionary<dynamic, dynamic> typeCahe))
            {
                typeCahe = new Dictionary<dynamic, dynamic>();
                caches.Add(type, typeCahe);
            }

            if(!typeCahe.TryGetValue(obj, out dynamic value))
            {
                value = transform(obj);
                typeCahe.Add(obj, value);
            }

            return value;
        }
    }
}

