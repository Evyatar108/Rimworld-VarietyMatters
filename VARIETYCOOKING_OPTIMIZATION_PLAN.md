# VarietyCooking Performance Optimization Plan

## Executive Summary

The [`VarietyCooking.cs`](Source/VarietyCooking.cs) file contains critical performance bottlenecks that significantly impact cooking operations in RimWorld colonies. This document outlines a comprehensive optimization strategy to address O(n²) algorithms, redundant component lookups, and inefficient caching patterns.

## Critical Performance Issues Identified

### 1. **O(n²) Sorting Algorithm** - Lines 152-194
**Location**: [`SortIngredients`](Source/VarietyCooking.cs:149) method comparison delegate

**Problem**: 
- Nested loops: For each ingredient pair comparison, loops through entire `curVarieties` list
- **Complexity**: O(n² × m) where n = available ingredients, m = recent recipes
- Called on **every cooking job evaluation**

**Current Code**:
```csharp
for (int i = 0; i < curVarieties.Count; i++)  // Inner loop for EVERY comparison
{
    bool flag2 = curVarieties[i] == t1.def.label;  // String comparison
    if (flag2)
    {
        num += 1f;
    }
    else
    {
        bool flag3 = curVarieties[i] == t2.def.label;  // Another string comparison
        if (flag3)
        {
            num2 += 1f;
        }
    }
}
```

**Impact**: With 50 ingredients and 10 recent recipes = 25,000 string comparisons per sort operation

### 2. **Redundant Component Lookups** - Lines 91, 144, 151
**Problem**: 
- `Chef.MapHeld.GetComponent<VarietyCookingRecord>()` called multiple times
- Map component lookups involve dictionary searches - expensive operations
- No caching between calls

**Current Code**:
```csharp
// Line 91
VarietyCooking.Chef.MapHeld.GetComponent<VarietyCookingRecord>().UpdateCookingRecord(chosen);

// Line 144  
VarietyCooking.Chef.MapHeld.GetComponent<VarietyCookingRecord>().UpdateCookingRecord(chosen);

// Line 151
List<string> curVarieties = VarietyCooking.Chef.MapHeld.GetComponent<VarietyCookingRecord>().CheckRecentRecipes(VarietyCooking.Chef);
```

**Impact**: 3× unnecessary component lookups per cooking operation

### 3. **Bug + Performance Issue in Rot Checking** - Line 180
**Problem**: 
- **Bug**: Both variables check the same thing (`t1` instead of `t2`)
- Component lookups (`TryGetComp<CompRottable>`) are expensive
- Done for every ingredient comparison

**Current Code**:
```csharp
CompRottable compRottable = ThingCompUtility.TryGetComp<CompRottable>(t1);
CompRottable compRottable2 = ThingCompUtility.TryGetComp<CompRottable>(t1);  // BUG: should be t2
```

**Impact**: Double component lookup + incorrect comparison logic

### 4. **Frequent Expensive Method Calls** - Line 151
**Problem**: 
- `CheckRecentRecipes(Chef)` called on every sort operation
- Likely performs collection lookups/database queries
- No caching of results

**Impact**: Redundant data retrieval on every cooking evaluation

## Optimization Strategy

### **Phase A: Algorithm Optimization**
**Target**: Reduce O(n²) complexity to O(n log n)

**Solution**: Pre-compute variety penalties using HashSet lookup
```csharp
// Pre-compute variety penalties once per sort
var varietyPenalties = new Dictionary<string, float>();
if (curVarieties != null)
{
    var varietySet = new HashSet<string>(curVarieties);
    foreach (var thing in availableThings)
    {
        varietyPenalties[thing.def.label] = varietySet.Contains(thing.def.label) ? 1f : 0f;
    }
}

// Simplified comparison: O(1) lookups
Comparison<Thing> comparison = (t1, t2) =>
{
    float penalty1 = varietyPenalties.GetValueOrDefault(t1.def.label, 0f);
    float penalty2 = varietyPenalties.GetValueOrDefault(t2.def.label, 0f);
    // ... rest of comparison logic
};
```

**Expected Improvement**: 95%+ reduction in comparison time complexity

### **Phase B: Component Lookup Optimization**
**Target**: Eliminate redundant component lookups within single operation

**Solution**: Cache component reference within method scope only
```csharp
// SAFE: Cache only within single cooking operation
private static bool BestVariety_AllowMix(ref bool __result, List<Thing> availableThings, Bill bill, List<ThingCount> chosen, IntVec3 rootCell)
{
    // Cache component for this operation only
    var cookingRecord = Chef?.MapHeld?.GetComponent<VarietyCookingRecord>();
    if (cookingRecord == null) return true;
    
    // Use cached reference for all calls within this method
    var curVarieties = cookingRecord.CheckRecentRecipes(Chef);
    // ... cooking logic ...
    cookingRecord.UpdateCookingRecord(chosen);
}
```

**Expected Improvement**: 67% reduction in component lookup calls **within single operation**

### **Phase C: Rot Checking Optimization**
**Target**: Fix bug and optimize component access

**Solution**: 
1. Fix the bug (use `t2` for second variable)
2. Cache component lookups in comparison delegate
```csharp
// Cache rot components to avoid repeated lookups
var rotCache = new Dictionary<Thing, CompRottable>();
Func<Thing, CompRottable> getRot = (thing) =>
{
    if (!rotCache.TryGetValue(thing, out var comp))
    {
        comp = ThingCompUtility.TryGetComp<CompRottable>(thing);
        rotCache[thing] = comp;
    }
    return comp;
};
```

**Expected Improvement**: 50% reduction in component lookup overhead + bug fix

### **Phase D: Caching Strategy**
**Target**: Cache expensive method results

**Solution**: Implement result caching with invalidation
```csharp
private static List<string> cachedRecentRecipes;
private static Pawn cachedRecipesPawn;
private static int cachedRecipesTick;

private static List<string> GetCachedRecentRecipes(Pawn chef)
{
    if (cachedRecipesPawn != chef || 
        cachedRecipesTick + 60 < Find.TickManager.TicksGame) // Cache for 1 second
    {
        cachedRecentRecipes = GetCookingRecord()?.CheckRecentRecipes(chef);
        cachedRecipesPawn = chef;
        cachedRecipesTick = Find.TickManager.TicksGame;
    }
    return cachedRecentRecipes;
}
```

**Expected Improvement**: 80% reduction in method call overhead

## Implementation Plan

### **Step 1: Create Performance Infrastructure**
- Create `VarietyCookingOptimizer` helper class
- Implement caching mechanisms
- Add performance monitoring hooks

### **Step 2: Optimize SortIngredients Method**
- Replace O(n²) algorithm with O(n log n) approach
- Implement variety penalty pre-computation
- Add rot component caching

### **Step 3: Implement Component Caching**
- Cache `VarietyCookingRecord` component
- Add cache invalidation logic
- Update all component access points

### **Step 4: Add Method Result Caching**
- Cache `CheckRecentRecipes` results
- Implement time-based cache invalidation
- Add cache clearing on relevant events

## Expected Performance Improvements

| Optimization | Performance Gain | Impact Area |
|-------------|------------------|-------------|
| Algorithm Optimization | 95% faster sorting | Cooking job evaluation |
| Component Caching | 67% fewer lookups | Memory access |
| Rot Checking Fix | 50% fewer component calls | Comparison operations |
| Method Caching | 80% fewer method calls | Data retrieval |

**Overall Expected Improvement**: 70-85% faster cooking operations

## Risk Assessment

### **Low Risk**:
- Component caching (standard RimWorld pattern)
- Method result caching (common optimization)

### **Medium Risk**:
- Algorithm changes (requires thorough testing)
- Cache invalidation logic (must handle edge cases)

### **Testing Requirements**:
1. **Cooking Variety**: Verify ingredient preference logic unchanged
2. **Performance**: Benchmark with large ingredient stockpiles
3. **Memory**: Check for cache memory leaks
4. **Multiplayer**: Ensure thread safety if applicable

## Implementation Files

### **New Files**:
- `Source/New/Performance/VarietyCookingOptimizer.cs` - Main optimization logic
- `Source/New/Performance/IngredientSortCache.cs` - Sorting optimization
- `Source/New/Performance/ComponentCache.cs` - Generic component caching

### **Modified Files**:
- `Source/VarietyCooking.cs` - Apply optimizations

## Monitoring and Validation

### **Performance Metrics**:
- Cooking job evaluation time
- Sort operation duration
- Component lookup frequency
- Memory usage patterns

### **Validation Tests**:
- Large colony cooking stress tests
- Ingredient variety preference verification
- Save/load compatibility checks
- Edge case handling (no chef, no ingredients, etc.)

## Conclusion

The VarietyCooking optimizations represent a critical performance improvement for the mod, addressing the most expensive operations in cooking job evaluation. The combination of algorithmic improvements and intelligent caching should provide substantial performance gains while maintaining full compatibility with existing functionality.

**Next Step**: Proceed with implementation phase focusing on highest-impact optimizations first.