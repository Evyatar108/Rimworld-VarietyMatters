# VarietyMatters Mod - Performance Optimization Plan

## Executive Summary

This document outlines a comprehensive performance optimization plan for the VarietyMatters RimWorld mod. The analysis identified critical performance bottlenecks primarily in string handling, tooltip generation, and memory management that can cause significant frame rate drops and UI lag.

## Performance Issues Analysis

### 🔴 Critical Issues (Severe Impact)

#### 1. String Allocation & Concatenation Problems
**Location**: [`Need_FoodVariety.cs:189-296`](Source/Need_FoodVariety.cs:189)
- **Issue**: Excessive string concatenation using `string.Concat()` arrays
- **Impact**: Called every UI frame when tooltip is visible
- **Severity**: SEVERE - Major UI lag
```csharp
// Problem code:
return string.Concat(new string[] {
    tipString, "\n\nI expect at least ", varietesCountExpectedText, 
    " different meals; ", "recently I had ", actualVarietiesCountText, "."
});
```

#### 2. Expensive Tooltip Generation
**Location**: [`Need_FoodVariety.cs:204-295`](Source/Need_FoodVariety.cs:204)
- **Issue**: Complex nested loops with O(n²) complexity
- **Impact**: Tooltip generation takes exponentially longer with more food variety
- **Severity**: SEVERE - Performance degrades with gameplay progression

#### 3. Inefficient Collection Operations
**Location**: [`DietTracker.cs:203-219`](Source/New/DietTracker.cs:203)
- **Issue**: Queue recreation instead of efficient item removal
- **Impact**: Linear search through entire queue for matching items
- **Severity**: MODERATE - Frequent operation causing cumulative slowdown

### 🟡 High Priority Issues

#### 4. LINQ Performance Problems
**Location**: [`VarietyRecord.cs:115`](Source/VarietyRecord.cs:115)
- **Issue**: Complex LINQ chains with multiple operations
- **Impact**: Save/load performance degradation
- **Severity**: MODERATE - Affects game state persistence

#### 5. Excessive Object Creation
**Location**: [`EatenFoodSource.cs:16-38`](Source/New/EatenFoodSource.cs:16)
- **Issue**: 12+ FastLazy objects created per food item
- **Impact**: Memory pressure and GC overhead
- **Severity**: MODERATE - Scales with food variety

#### 6. Inefficient Caching Strategy
**Location**: [`CompVariety.cs:45-70`](Source/CompVariety.cs:45)
- **Issue**: Missing cache invalidation and inconsistent caching
- **Impact**: Potential memory leaks and stale data
- **Severity**: MODERATE - Long-term stability issues

### 🟢 Medium Priority Issues

#### 7. Settings UI Performance
**Location**: [`Mod_VarietyMatters.cs:94-99`](Source/Mod_VarietyMatters.cs:94)
- **Issue**: Immediate full recalculation on setting changes
- **Impact**: UI freezing during configuration
- **Severity**: LOW - Infrequent but noticeable

#### 8. Food Analysis Bottleneck
**Location**: [`CompVariety.cs:93-151`](Source/CompVariety.cs:93)
- **Issue**: Full DefDatabase iteration on game start
- **Impact**: Longer loading times
- **Severity**: LOW - One-time cost per game session

## Optimization Plan

### Phase 1: Critical Performance Fixes (Week 1-2)

#### Priority 1.1: Tooltip String Caching
**Target**: [`Need_FoodVariety.cs:171-197`](Source/Need_FoodVariety.cs:171)

**Optimizations**:
- Implement tooltip result caching with invalidation triggers
- Use StringBuilder pooling for string construction
- Cache expensive random string selections

**Implementation**:
```csharp
public class TooltipCache {
    private string cachedTooltip;
    private int lastVarietyCount = -1;
    private int lastExpectation = -1;
    
    public string GetCachedTooltip(Need_FoodVariety need) {
        if (ShouldInvalidateCache(need)) {
            cachedTooltip = GenerateTooltip(need);
            UpdateCacheKeys(need);
        }
        return cachedTooltip;
    }
}
```

**Expected Improvement**: 85-95% reduction in tooltip generation time

#### Priority 1.2: Food Description Optimization
**Target**: [`Need_FoodVariety.cs:204-295`](Source/Need_FoodVariety.cs:204)

**Optimizations**:
- Pre-compute food descriptions during ingestion
- Implement incremental updates instead of full regeneration
- Use object pooling for StringBuilder instances

**Implementation**:
```csharp
public class PrecomputedFoodDescription {
    private readonly Dictionary<string, string> descriptionCache = new();
    private readonly StringBuilder pooledBuilder = new(256);
    
    public string GetDescription(EatenFoodSource food) {
        if (!descriptionCache.TryGetValue(food.GetFoodSourceKey(), out string desc)) {
            desc = GenerateDescription(food);
            descriptionCache[food.GetFoodSourceKey()] = desc;
        }
        return desc;
    }
}
```

**Expected Improvement**: 70-80% reduction in description generation time

#### Priority 1.3: Collection Operation Efficiency
**Target**: [`DietTracker.cs:203-219`](Source/New/DietTracker.cs:203)

**Optimizations**:
- Replace Queue recreation with efficient removal algorithms
- Use HashSet for O(1) lookups where possible
- Implement lazy evaluation for expensive operations

**Implementation**:
```csharp
public void RemoveFoodFromQueueByKey(string key) {
    var toRemove = new List<EatenFoodByPawn>();
    foreach (var item in foodSourcesByOrderV2) {
        if (item.Source.GetFoodSourceKey() == key) {
            toRemove.Add(item);
        }
    }
    
    foreach (var item in toRemove) {
        RemoveFoodFromCount(item);
        foodSourcesByOrderV2 = new Queue<EatenFoodByPawn>(
            foodSourcesByOrderV2.Where(x => x != item)
        );
    }
}
```

**Expected Improvement**: 60-70% reduction in queue operation time

### Phase 2: Structural Improvements (Week 3-4)

#### Priority 2.1: FastLazy Object Consolidation
**Target**: [`EatenFoodSource.cs:16-38`](Source/New/EatenFoodSource.cs:16)

**Optimizations**:
- Consolidate related computations into single lazy objects
- Use static caching for commonly accessed values
- Implement string interning for repeated values

**Implementation**:
```csharp
public class ConsolidatedFoodStrings {
    private readonly FastLazy<FoodStringSet> allStrings;
    
    public ConsolidatedFoodStrings(EatenFoodSource source) {
        allStrings = new FastLazy<FoodStringSet>(() => 
            GenerateAllStrings(source));
    }
    
    public string MealAsString => allStrings.Value.MealString;
    public string IngredientsAsString => allStrings.Value.IngredientsString;
    // ... other properties
}
```

**Expected Improvement**: 40-50% reduction in object allocation

#### Priority 2.2: LINQ Optimization
**Target**: [`VarietyRecord.cs:115`](Source/VarietyRecord.cs:115)

**Optimizations**:
- Replace complex LINQ chains with iterative solutions
- Use ToList() to materialize expensive enumerations
- Implement custom collection operations where needed

**Implementation**:
```csharp
// Replace this LINQ chain:
// eatenFoodSources = DietRecord?.Values?.SelectMany(x => x.EatenFoodSourcesByOrder)
//     ?.Select(x => x.Source).Distinct(EatenFoodSourceEqualityComparer.Instance)?.ToList()

// With optimized version:
private List<EatenFoodSource> GetDistinctEatenFoodSources() {
    var seen = new HashSet<string>();
    var result = new List<EatenFoodSource>();
    
    if (DietRecord?.Values != null) {
        foreach (var tracker in DietRecord.Values) {
            foreach (var eaten in tracker.EatenFoodSourcesByOrder) {
                if (seen.Add(eaten.Source.GetUniqueLoadID())) {
                    result.Add(eaten.Source);
                }
            }
        }
    }
    return result;
}
```

**Expected Improvement**: 30-40% improvement in save/load performance

#### Priority 2.3: Settings UI Debouncing
**Target**: [`Mod_VarietyMatters.cs:91-106`](Source/Mod_VarietyMatters.cs:91)

**Optimizations**:
- Add debouncing to setting changes
- Implement async recalculation for heavy operations
- Cache setting-dependent calculations

**Implementation**:
```csharp
public class DebouncedSettingsHandler {
    private static int pendingRecalculations = 0;
    private static readonly object lockObj = new object();
    
    public static void QueueRecalculation() {
        lock (lockObj) {
            pendingRecalculations++;
            
            // Debounce: only process after 500ms of no changes
            Timer timer = new Timer(_ => {
                lock (lockObj) {
                    if (--pendingRecalculations == 0) {
                        PerformRecalculation();
                    }
                }
            }, null, 500, Timeout.Infinite);
        }
    }
}
```

**Expected Improvement**: Elimination of UI freezing

### Phase 3: Memory Optimizations (Week 5-6)

#### Priority 3.1: Object Pooling Implementation
**Target**: Various allocation-heavy classes

**Optimizations**:
- Implement object pooling for frequently created objects
- Use struct types where appropriate to reduce allocations
- Add proper disposal patterns for large objects

**Implementation**:
```csharp
public static class StringBuilderPool {
    private static readonly ConcurrentQueue<StringBuilder> pool = new();
    
    public static StringBuilder Get() {
        if (pool.TryDequeue(out StringBuilder sb)) {
            sb.Clear();
            return sb;
        }
        return new StringBuilder(256);
    }
    
    public static void Return(StringBuilder sb) {
        if (sb.Capacity <= 1024) { // Prevent memory bloat
            pool.Enqueue(sb);
        }
    }
}
```

**Expected Improvement**: 50-60% reduction in GC pressure

#### Priority 3.2: Caching Strategy Overhaul
**Target**: [`CompVariety.cs`](Source/CompVariety.cs), various caching points

**Optimizations**:
- Implement LRU cache with size limits
- Add cache invalidation triggers
- Use weak references for large cached objects

**Implementation**:
```csharp
public class LRUCache<TKey, TValue> {
    private readonly int maxSize;
    private readonly Dictionary<TKey, LinkedListNode<CacheItem>> dict;
    private readonly LinkedList<CacheItem> list;
    
    public bool TryGet(TKey key, out TValue value) {
        if (dict.TryGetValue(key, out var node)) {
            list.Remove(node);
            list.AddFirst(node);
            value = node.Value.Value;
            return true;
        }
        value = default;
        return false;
    }
    
    public void Add(TKey key, TValue value) {
        if (dict.Count >= maxSize) {
            var last = list.Last;
            list.RemoveLast();
            dict.Remove(last.Value.Key);
        }
        
        var newNode = new LinkedListNode<CacheItem>(new CacheItem(key, value));
        list.AddFirst(newNode);
        dict[key] = newNode;
    }
}
```

**Expected Improvement**: Better memory utilization and cache hit rates

## Implementation Timeline

### Week 1-2: Critical Fixes
- [ ] Implement tooltip caching system
- [ ] Optimize food description generation
- [ ] Improve collection operations
- [ ] Test and validate critical path performance

### Week 3-4: Structural Improvements
- [ ] Consolidate FastLazy objects
- [ ] Optimize LINQ usage
- [ ] Implement settings debouncing
- [ ] Performance testing and profiling

### Week 5-6: Memory Optimizations
- [ ] Implement object pooling
- [ ] Overhaul caching strategy
- [ ] Final performance validation
- [ ] Documentation and code review

## Success Metrics

### Performance Targets
- **Tooltip Generation**: 80-90% reduction in generation time
- **Memory Allocations**: 40-60% reduction in GC pressure
- **Save/Load Performance**: 30-50% improvement in persistence operations
- **UI Responsiveness**: Elimination of freezing during settings changes
- **Overall Mod Impact**: <1ms per frame for typical colony sizes

### Testing Methodology
1. **Baseline Measurements**: Profile current performance with various colony sizes
2. **Incremental Testing**: Validate each optimization phase independently
3. **Regression Testing**: Ensure functionality remains intact
4. **Load Testing**: Test with maximum expected food variety scenarios
5. **Memory Profiling**: Monitor allocation patterns and GC behavior

## Risk Assessment

### Low Risk Optimizations
- String caching and pooling
- Collection algorithm improvements
- Settings debouncing

### Medium Risk Optimizations
- LINQ replacements
- Object pooling implementation
- Cache strategy changes

### High Risk Optimizations
- FastLazy consolidation (may affect serialization)
- Major architectural changes

## Conclusion

This optimization plan targets the most impactful performance issues first, providing immediate benefits to users while establishing a foundation for long-term performance improvements. The phased approach allows for incremental validation and reduces the risk of introducing functional regressions.

The expected overall improvement should result in a significantly more responsive mod that scales better with colony size and food variety complexity, enhancing the player experience across all game scenarios.