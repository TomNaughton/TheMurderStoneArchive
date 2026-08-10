# Code Duplication and Redundancy Analysis
## TheMurderStoneArchive Project

Date: Generated from workspace analysis  
Project Type: ASP.NET Core Razor Pages (.NET 10)

---

## Executive Summary

Found **3 major areas of code duplication** and several patterns of redundancy that should be refactored.

---

## Critical Duplications

### 1. **File Validation Logic - HIGHEST PRIORITY**
**Severity:** High | **Files Affected:** `MurderEventsController.cs` (Lines 292-346)
**Impact:** Code appears **TWICE** (duplicated validation loop)

#### Issue:
File validation for photos is performed twice identically in the `Create()` method before saving:
- First validation loop: Lines 292-314
- Second validation loop: Lines 317-339

**Duplicated Logic:**
```csharp
// Appears twice:
foreach (var file in Photos)
{
	if (file.Length == 0) continue;
	if (file.Length > MaxFileSize)
		ModelState.AddModelError("Photos", $"File {file.FileName} exceeds the {MaxFileSize / (1024*1024)} MB limit.");
	if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
		ModelState.AddModelError("Photos", $"File {file.FileName} is not a valid image.");
	var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
	if (!allowedExt.Contains(ext))
		ModelState.AddModelError("Photos", $"File {file.FileName} has an unsupported extension.");
}
```

**Recommendation:**
- Extract to private helper method: `private void ValidatePhotoFiles(List<IFormFile> photos, string[] allowedExt, long maxFileSize)`
- Call once before validation

---

### 2. **Redundant Validation Calls - HIGH PRIORITY**
**Severity:** High | **File:** `MurderEventsController.cs` (Lines 343, 288)
**Impact:** Same validation error check performed multiple times

#### Issue:
`ConfirmRightsAndTerms` validation appears **twice** in the `Create()` method:
- Line 288
- Line 343

**Duplicated Logic:**
```csharp
if (!murderEvent.ConfirmRightsAndTerms)
{
	ModelState.AddModelError("ConfirmRightsAndTerms", "You must confirm...");
}
```

**Recommendation:**
- Keep only one validation before the property checks
- Remove the second occurrence (Line 343)

---

### 3. **YouTube Video Processing Logic - HIGH PRIORITY**
**Severity:** High | **File:** `MurderEventsController.cs` (Lines 403-430, 443-456)
**Impact:** YouTube link processing appears **TWO TIMES** with minor variations

#### Issue:
Identical logic for processing YouTube links appears twice:

**First instance (Lines 403-419):**
```csharp
var youTubeLinks = Request.Form["YouTubeLinks"].ToArray();
if (youTubeLinks != null)
{
	var existingVideos = _context.MurderEventVideos.Where(v => v.MurderEventId == murderEvent.Id);
	_context.MurderEventVideos.RemoveRange(existingVideos);
	var added = 0;
	foreach (var link in youTubeLinks)
	{
		if (added >= 3) break;
		if (string.IsNullOrWhiteSpace(link)) continue;
		var vid = ExtractYouTubeId(link);
		if (vid == null) continue;
		_context.MurderEventVideos.Add(new Models.MurderEventVideo { MurderEventId = murderEvent.Id, Url = link, VideoId = vid });
		added++;
	}
}
```

**Second instance (Lines 443-456):** Same logic but using `YouTubeLinks` parameter

**Recommendation:**
- Extract to private helper method: `private async Task ProcessYouTubeLinksAsync(int murderEventId, List<string> links)`
- Use this method in both Create and Edit actions
- Remove duplicate code

---

### 4. **Database Query Filtering - MEDIUM PRIORITY**
**Severity:** Medium | **Files Affected:** Multiple controllers
**Impact:** Same WHERE filtering repeated across queries

#### Issue:
The filter `.Where(m => m.IsApproved && !m.IsLost)` appears in multiple places:
- `HomeController.cs` - Lines 16, 49 (Index and Sitemap methods)
- `SearchController.cs` - Line 26
- `MurderEventsController.cs` - Various read operations

**Duplicated Pattern:**
```csharp
.Where(m => m.IsApproved && !m.IsLost) // Appears 5+ times across controllers
```

**Recommendation:**
- Create extension method in `ApplicationDbContext` or as a static utility:
  ```csharp
  public static IQueryable<MurderEvent> ApprovedAndNotLost(this IQueryable<MurderEvent> query)
	  => query.Where(m => m.IsApproved && !m.IsLost);
  ```
- Usage: `.ApprovedAndNotLost()` instead of repeating the WHERE clause

---

### 5. **Include() Chains - MEDIUM PRIORITY**
**Severity:** Medium | **File:** `MurderEventsController.cs`
**Impact:** Similar Include patterns without consolidation

#### Issue:
Include chains are repeated in Create, Edit, and Details methods:
- Line 81: `.Include(m => m.Location)` - repeated
- Lines 250-254: Multi-include chain `.Include(m => m.Location).Include(m => m.Monuments)...`
- Line 476-478: Similar pattern

**Recommendation:**
- Create extension method:
  ```csharp
  public static IQueryable<MurderEvent> WithRelations(this IQueryable<MurderEvent> query)
	  => query.Include(m => m.Location)
			  .Include(m => m.Monuments)
			  .Include(m => m.Perpetrators)
			  .Include(m => m.Photos)
			  .Include(m => m.Videos);

  public static IQueryable<MurderEvent> WithBasicRelations(this IQueryable<MurderEvent> query)
	  => query.Include(m => m.Location);
  ```

---

## Secondary Issues (Code Quality)

### 6. **Photo Validation Constants - MEDIUM**
**File:** `MurderEventsController.cs`
**Issue:** Constants defined locally in method (Line 280-281) instead of class-level

```csharp
// Should be class-level constants:
const long MaxFileSize = 25L * 1024L * 1024L;
var allowedExt = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
```

**Recommendation:**
```csharp
private static class PhotoValidation
{
	public const long MaxFileSize = 25L * 1024L * 1024L;
	public static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
	public const int MaxFiles = 10;
}
```

---

### 7. **Constructor Inconsistency - LOW**
**Severity:** Low | **Files:** `HomeController.cs`, `SearchController.cs`

#### Issue:
Different constructor styles used:
- `HomeController.cs` (Line 9): `public HomeController(ApplicationDbContext context) => _context = context;`
- `SearchController.cs` (Line 10): Same pattern
- `MurderEventsController.cs`: Traditional multi-line constructor

**Recommendation:** Normalize all to expression-bodied property initialization where applicable

---

### 8. **Razor View Patterns - LOW**
**Severity:** Low | **Files:** `Views/MurderEvents/Submit.cshtml` (Lines 107-165)
**Issue:** Similar file preview/removal JavaScript logic that could be extracted to a shared script

---

## Summary of Duplications by Type

| Type | Count | Severity | Files |
|------|-------|----------|-------|
| File Validation Loops | 2 instances | High | MurderEventsController.cs |
| YouTube Link Processing | 2 instances | High | MurderEventsController.cs |
| ConfirmRightsAndTerms Validation | 2 instances | High | MurderEventsController.cs |
| IsApproved && !IsLost Filter | 5+ instances | Medium | Multiple Controllers |
| Include() Chains | 3+ instances | Medium | MurderEventsController.cs |
| Magic Numbers/Constants | Multiple | Medium | MurderEventsController.cs |

---

## Refactoring Priority

1. **URGENT:** Extract photo validation to helper method
2. **URGENT:** Extract YouTube link processing to helper method
3. **HIGH:** Remove duplicate ConfirmRightsAndTerms check
4. **HIGH:** Create query extension methods for common filters
5. **MEDIUM:** Extract constants to class-level
6. **MEDIUM:** Create Where() extension for approval/lost filtering
7. **LOW:** Standardize constructor patterns
8. **LOW:** Extract JavaScript logic to shared files

---

## Estimated Impact

- **Lines reduced:** ~100-150 lines of duplicate code
- **Maintainability gain:** High - easier to fix bugs in single location
- **Test coverage improvement:** Easier to unit test extracted helpers
- **Performance:** Neutral to slight improvement (fewer repeated allocations)

---

## Related Files for Review
- `TheMurderStoneArchive/Controllers/MurderEventsController.cs` (816 lines - review for further refactoring)
- `TheMurderStoneArchive/Controllers/HomeController.cs`
- `TheMurderStoneArchive/Controllers/SearchController.cs`
- `TheMurderStoneArchive/Data/ApplicationDbContext.cs` (consider extension methods)
