﻿using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Linq;
using System.Globalization;
using System.ComponentModel;
using System.Collections;

namespace LeanCloud.Storage.Internal
{
    /// <summary>
    /// So here's the deal. We have a lot of internal APIs for AVObject, AVUser, etc.
    ///
    /// These cannot be 'internal' anymore if we are fully modularizing things out, because
    /// they are no longer a part of the same library, especially as we create things like
    /// Installation inside push library.
    ///
    /// So this class contains a bunch of extension methods that can live inside another
    /// namespace, which 'wrap' the intenral APIs that already exist.
    /// </summary>
    public static class AVObjectExtensions
    {
        public static T FromState<T>(IObjectState state, string defaultClassName) where T : AVObject
        {
            return AVObject.FromState<T>(state, defaultClassName);
        }

        public static IObjectState GetState(this AVObject obj)
        {
            return obj.State;
        }

        public static void HandleFetchResult(this AVObject obj, IObjectState serverState)
        {
            obj.HandleFetchResult(serverState);
        }

        public static IDictionary<string, IAVFieldOperation> GetCurrentOperations(this AVObject obj)
        {
            return obj.CurrentOperations;
        }

        public static IDictionary<string, object> Encode(this AVObject obj)
        {
            return PointerOrLocalIdEncoder.Instance.EncodeAVObject(obj, false);
        }

        public static IEnumerable<object> DeepTraversal(object root, bool traverseAVObjects = false, bool yieldRoot = false)
        {
            return AVObject.DeepTraversal(root, traverseAVObjects, yieldRoot);
        }

        public static void SetIfDifferent<T>(this AVObject obj, string key, T value)
        {
            obj.SetIfDifferent<T>(key, value);
        }

        public static IDictionary<string, object> ServerDataToJSONObjectForSerialization(this AVObject obj)
        {
            return obj.ServerDataToJSONObjectForSerialization();
        }

        public static void Set(this AVObject obj, string key, object value)
        {
            obj.Set(key, value);
        }

        public static void DisableHooks(this AVObject obj, IEnumerable<string> hookKeys)
        {
            obj.Set("__ignore_hooks", hookKeys);
        }
        public static void DisableHook(this AVObject obj, string hookKey)
        {
            var newList = new List<string>();
            if (obj.ContainsKey("__ignore_hooks"))
            {
                var hookKeys = obj.Get<IEnumerable<string>>("__ignore_hooks");
                newList = hookKeys.ToList();
            }
            newList.Add(hookKey);
            obj.DisableHooks(newList);
        }

        public static void DisableAfterHook(this AVObject obj)
        {
            obj.DisableAfterSave();
            obj.DisableAfterUpdate();
            obj.DisableAfterDelete();
        }

        public static void DisableBeforeHook(this AVObject obj)
        {
            obj.DisableBeforeSave();
            obj.DisableBeforeDelete();
            obj.DisableBeforeUpdate();
        }

        public static void DisableBeforeSave(this AVObject obj)
        {
            obj.DisableHook("beforeSave");
        }
        public static void DisableAfterSave(this AVObject obj)
        {
            obj.DisableHook("afterSave");
        }
        public static void DisableBeforeUpdate(this AVObject obj)
        {
            obj.DisableHook("beforeUpdate");
        }
        public static void DisableAfterUpdate(this AVObject obj)
        {
            obj.DisableHook("afterUpdate");
        }
        public static void DisableBeforeDelete(this AVObject obj)
        {
            obj.DisableHook("beforeDelete");
        }
        public static void DisableAfterDelete(this AVObject obj)
        {
            obj.DisableHook("afterDelete");
        }

        #region on property updated or changed or collection updated

        /// <summary>
        /// On the property changed.
        /// </summary>
        /// <param name="avObj">Av object.</param>
        /// <param name="propertyName">Property name.</param>
        /// <param name="handler">Handler.</param>
        public static void OnPropertyChanged(this AVObject avObj, string propertyName, PropertyChangedEventHandler handler)
        {
            avObj.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == propertyName)
                {
                    handler(sender, e);
                }
            };
        }

        /// <summary>
        /// On the property updated.
        /// </summary>
        /// <param name="avObj">Av object.</param>
        /// <param name="propertyName">Property name.</param>
        /// <param name="handler">Handler.</param>
        public static void OnPropertyUpdated(this AVObject avObj, string propertyName, PropertyUpdatedEventHandler handler)
        {
            avObj.PropertyUpdated += (sender, e) =>
            {
                if (e.PropertyName == propertyName)
                {
                    handler(sender, e);
                }
            };
        }

        /// <summary>
        /// On the property updated.
        /// </summary>
        /// <param name="avObj">Av object.</param>
        /// <param name="propertyName">Property name.</param>
        /// <param name="handler">Handler.</param>
        public static void OnPropertyUpdated(this AVObject avObj, string propertyName, Action<object, object> handler)
        {
            avObj.OnPropertyUpdated(propertyName,(object sender, PropertyUpdatedEventArgs e) => 
            {
                handler(e.OldValue, e.NewValue);
            });
        }

        /// <summary>
        /// On the collection property updated.
        /// </summary>
        /// <param name="avObj">Av object.</param>
        /// <param name="propertyName">Property name.</param>
        /// <param name="handler">Handler.</param>
        public static void OnCollectionPropertyUpdated(this AVObject avObj, string propertyName, CollectionPropertyUpdatedEventHandler handler)
        {
            avObj.CollectionPropertyUpdated += (sender, e) =>
            {
                if (e.PropertyName == propertyName)
                {
                    handler(sender, e);
                }
            };
        }

        /// <summary>
        /// On the collection property added.
        /// </summary>
        /// <param name="avObj">Av object.</param>
        /// <param name="propertyName">Property name.</param>
        /// <param name="handler">Handler.</param>
        public static void OnCollectionPropertyAdded(this AVObject avObj, string propertyName, Action<IEnumerable> handler)
        {
            avObj.OnCollectionPropertyUpdated(propertyName, (sender, e) =>
            {
                if (e.CollectionAction == NotifyCollectionUpdatedAction.Add)
                {
                    handler(e.NewValues);
                }
            });
        }

        /// <summary>
        /// On the collection property removed.
        /// </summary>
        /// <param name="avObj">Av object.</param>
        /// <param name="propertyName">Property name.</param>
        /// <param name="handler">Handler.</param>
        public static void OnCollectionPropertyRemoved(this AVObject avObj, string propertyName, Action<IEnumerable> handler)
        {
            avObj.OnCollectionPropertyUpdated(propertyName, (sender, e) =>
            {
                if (e.CollectionAction == NotifyCollectionUpdatedAction.Remove)
                {
                    handler(e.NewValues);
                }
            });
        }
        #endregion
    }
}