using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;


namespace FSTRaK.Utils
{
    public static class CollectionUtils
    {
        public static void ReplaceContent<T>(this BindingList<T> bindingList, IEnumerable<T> newContentList)
        {
            if (bindingList == null)
            {
                throw new ArgumentNullException(nameof(bindingList));
            }

            if (newContentList == null)
            {
                throw new ArgumentNullException(nameof(newContentList));
            }

            bindingList.RaiseListChangedEvents = false;
            bindingList.Clear();
            foreach (T item in newContentList)
            {
                bindingList.Add(item);
            }

            bindingList.RaiseListChangedEvents = true;
            bindingList.ResetBindings();
        }
    }
}
