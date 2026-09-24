# Enum IdGranularity {#AdCodicem_ValueObjects_Identifiers_IdGranularity}

Namespace: [AdCodicem.ValueObjects.Identifiers](AdCodicem.ValueObjects.Identifiers.md)  
Assembly: AdCodicem.ValueObjects.Identifiers.dll  

The width of the time bucket that heads the body of an entity identifier.

```csharp
public enum IdGranularity
```

## Fields

`Minute = 0` 

One bucket per minute. Six characters, usable until the year 4062.



`Hour = 1` 

One bucket per hour. Four characters, usable until the year 2139. The default.



`Day = 2` 

One bucket per day. Three characters, usable until the year 2109.



## Remarks

<p>
The bucket exists to give the index a monotonic head, so that inserts land at the right edge of the B-tree
instead of scattering across it. It leaks the creation time of the identifier at exactly this granularity
and nothing finer; the random part keeps its full 80 bits either way, so enumeration is unaffected.
</p>
<p>
Choose it from the insert rate of the table, not from taste: aim for a bucket holding roughly 10⁴–10⁵ rows.
Wider and writes scatter again, narrower and the identifier leaks more precisely than it needs to.
</p>

