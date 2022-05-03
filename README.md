# Entities.Exposed
Helpful extensions for Entities and NativeContainers

Unity.Entities.Exposed namespace:

The main purpose is to get pointers and references from ComponentDataFromEntity

```
using Unity.Entities.Exposed;
```

Inside System - change:
```
  var Health_WriteLookup = GetComponentDataFromEntity<Health>(false);
```
to:
```
  var Health_WriteLookup =EntityManager.GetExposedCDFE<Health>(false);
```
  
Now you can query health and get a ref:
```
ref var healthComp = ref Health_WriteLookup.GetRef(lookupEntity);
healthComp.health += 100;
```

NZNativeContainers.Extensions namespace:

A bunch of extensions for NativeList and NativeMultiHashMap.
Core parts were written by [tertle](https://forum.unity.com/members/tertle.33474/)

NativeList.ParallelWriter has an extension method GetExtendedList(int increaseCount = 10) to get a NativeListExtended version from it.
A NativeListExtended doesn't allocate memory on every Add, instead it reserves blocks of elements, defined by the increaseCount.
With this, several threads can write to a NativeList without getting stalled by Interlocked increasing the length.

Best usage is when the final length is known. If not, another helper extension, FillEmpty can be used to fill in those elements at the end of a Job.

NativeMultiHashMap can be allocated in advance and with a call to GetKeyAndValueLists(out keys, out values), 2 NativeLists can be created that 
can be used to directly write to the keys and values.
Paired with the NativeListExtended this is a very fast way to produce NativeMultiHashMap data.
At the end of writing, CalculateBuckets needs to be called.

Also provided is an extension method, GetRefValuesForKey which will return an enumerator that is able to get ref values from the NativeMultiHashMap.
