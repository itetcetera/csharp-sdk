﻿using System;
using System.Collections.Generic;

namespace LeanCloud.Storage.Internal
{
    // TODO: (richardross) refactor entire LeanCloud coder interfaces.
    public class AVObjectCoder
    {
        private static readonly AVObjectCoder instance = new AVObjectCoder();
        public static AVObjectCoder Instance
        {
            get
            {
                return instance;
            }
        }

        // Prevent default constructor.
        private AVObjectCoder() { }

        public IDictionary<string, object> Encode<T>(T state,
            IDictionary<string, IAVFieldOperation> operations,
            AVEncoder encoder) where T : IObjectState
        {
            var result = new Dictionary<string, object>();
            foreach (var pair in operations)
            {
                // AVRPCSerialize the data
                var operation = pair.Value;

                result[pair.Key] = encoder.Encode(operation);
            }

            return result;
        }

        public IObjectState Decode(IDictionary<string, object> data,
            AVDecoder decoder)
        {
            IDictionary<string, object> serverData = new Dictionary<string, object>();
            var mutableData = new Dictionary<string, object>(data);
            string objectId = extractFromDictionary<string>(mutableData, "objectId", (obj) =>
            {
                return obj as string;
            });
            DateTime? createdAt = extractFromDictionary<DateTime?>(mutableData, "createdAt", (obj) =>
            {
                return (DateTime)obj;
            });
            DateTime? updatedAt = extractFromDictionary<DateTime?>(mutableData, "updatedAt", (obj) =>
            {
                return (DateTime)obj;
            });

            if (mutableData.ContainsKey("ACL"))
            {
                serverData["ACL"] = extractFromDictionary<AVACL>(mutableData, "ACL", (obj) =>
                {
                    return new AVACL(obj as IDictionary<string, object>);
                });
            }
            string className = extractFromDictionary<string>(mutableData, "className", obj =>
            {
                return obj as string;
            });
            if (createdAt != null && updatedAt == null)
            {
                updatedAt = createdAt;
            }

            // Bring in the new server data.
            foreach (var pair in mutableData)
            {
                if (pair.Key == "__type" || pair.Key == "className")
                {
                    continue;
                }

                var value = pair.Value;
                serverData[pair.Key] = decoder.Decode(value);
            }

            return new MutableObjectState
            {
                ObjectId = objectId,
                CreatedAt = createdAt,
                UpdatedAt = updatedAt,
                ServerData = serverData,
                ClassName = className
            };
        }

        private T extractFromDictionary<T>(IDictionary<string, object> data, string key, Func<object, T> action)
        {
            T result = default(T);
            if (data.ContainsKey(key))
            {
                result = action(data[key]);
                data.Remove(key);
            }

            return result;
        }
    }
}