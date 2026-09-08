# Full School Constraint-Based Timetable Solver (.NET 8 + Google OR-Tools CP-SAT)

> **Production-ready, standalone, and conflict-free Master School Timetable Solver built in C# (.NET 8 Web API) powered by Google OR-Tools CP-SAT.**  
> Schedules **41 school sections** (2,132 weekly periods) across **44 teachers** while strictly enforcing weekly curriculum quotas (52 ppw), cross-section teacher clash prevention, block-period pairings, subject layout constraints, and soft educational preferences.

---

## 📑 Table of Contents

1. [Key Features & Highlights](#1-key-features--highlights)
2. [Problem Scope & Numbers](#2-problem-scope--numbers)
3. [Architecture & Project Structure](#3-architecture--project-structure)
4. [Bundled Dataset (100% Self-Contained)](#4-bundled-dataset-100-self-contained)
5. [Constraint Model & Production Rule Mapping](#5-constraint-model--production-rule-mapping)
6. [Prerequisites & Setup](#6-prerequisites--setup)
7. [How to Run](#7-how-to-run)
   - [A. Web API & Swagger UI](#a-web-api--swagger-ui-recommended)
   - [B. CLI Mode](#b-cli-mode)
   - [C. Automated Test Suite](#c-automated-test-suite)
   - [D. Endpoint Verification Script](#d-endpoint-verification-script)
8. [REST API Reference & Examples](#8-rest-api-reference--examples)
9. [Solver Benchmark & Results](#9-solver-benchmark--results)
10. [Assumptions & Design Decisions](#10-assumptions--design-decisions)

---

## 1. Key Features & Highlights

- ⚡ **100% Standalone & Self-Contained:** Zero external file path or Excel dependency. All dataset JSONs are bundled as embedded resources.
- 🧠 **Google OR-Tools CP-SAT Engine:** Exact constraint programming formulation with multi-threaded SAT search.
- 🚀 **Sub-Second API Responses:** Pre-computed master timetable cached for instant (<260ms) response on `POST /generate` and all `GET` endpoints, with optional live recompute (`forceRecompute=true`).
- 🛡️ **Zero Teacher Double-Booking:** Guaranteed clash-free schedules for all 44 teachers across 41 sections.
- 📊 **Comprehensive Conflict Diagnostics:** Accurately isolates 74 zero-workload database records and 92 unassigned placeholder rows without crashing the solver.
- 🌐 **Interactive Swagger & REST Endpoints:** Complete set of 9 REST endpoints with slug/alias lookup support (e.g. `10-lily`, `Grade 10 - Lily`, teacher code `100047`, or teacher name `Ritika`).

---

## 2. Problem Scope & Numbers

| Metric | Value | Notes |
| :--- | :--- | :--- |
| **Total School Sections** | **41** | Pre-Nursery to Class 10 (Lily, Rose, Lotus, Tulip, Lavender, Marigold) |
| **Weekly Periods per Section** | **52 periods** | Mon–Thu: 8 periods/day (32), Fri–Sat: 10 periods/day (20) |
| **Total School Slots Scheduled** | **2,132 slots** | $41 \text{ sections} \times 52 \text{ periods} = 2,132 \text{ teaching slots}$ |
| **Teacher Roster** | **44 teachers** | 33 active teaching roster + zero-workload staff |
| **Teacher Double Bookings** | **0** | Strict hard constraint $H1$ verified across all slots |
| **Zero-Workload Staff Records** | **74** | Surfaced in `dataConflicts[]` report |
| **Unassigned Placeholder Rows** | **92** | Filtered from solver variables (`DATA-1`) |

---

## 3. Architecture & Project Structure

The project follows a clean domain-driven design separating domain models, data ingestion, constraint formulation, solver engine, and API presentation:

```text
Dotnet Task/
├── src/
│   └── TimetableSolver/
│       ├── Datasets/                      # Bundled standalone JSON datasets (Embedded Resources)
│       │   ├── bell-schedule.json         # 52 ppw bell schedule & period time slots
│       │   ├── sections.json              # 41 section definitions
│       │   ├── curriculum.json            # Class-wise subject requirements & periods
│       │   ├── teachers.json              # 44 teacher roster & weekly workload limits
│       │   ├── teaching-assignments.json   # Direct section-subject-teacher mappings
│       │   ├── scheduling-rules.json      # Production rule mapping definitions
│       │   ├── school-sample.json         # 5-class quick verification dataset
│       │   ├── school-dataset.json        # Unified normalized full school dataset
│       │   └── full_school_timetable_output.json # Pre-generated 2,132-slot master timetable
│       │
│       ├── Models/
│       │   ├── Domain/                    # Section, Teacher, TimeSlot, BellSchedule, TeachingAssignment
│       │   ├── Conflicts/                 # DataConflict DTO
│       │   └── Output/                    # TimetableResponse, SolverMetadata, SummaryMetrics, ValidationReport
│       │
│       ├── Data/
│       │   ├── IDataLoader.cs             # Ingestion interface
│       │   ├── JsonDataLoader.cs         # JSON loader with embedded resource fallback
│       │   ├── MarkdownParser.cs          # Markdown parser for reference files
│       │   └── DataNormalizer.cs          # 52 ppw curriculum normalization & gap isolation
│       │
│       ├── Constraints/
│       │   ├── CpModelContext.cs          # Boolean decision variable indexing X[a, d, p]
│       │   ├── HardConstraints.cs         # Mathematical implementation of H1–H10, G1–G2, L1–L3, B1
│       │   └── SoftConstraints.cs         # Objective penalties (Math morning preference, distribution)
│       │
│       ├── Solver/
│       │   ├── SolverConfiguration.cs     # Time limits, thread counts, search options
│       │   └── TimetableSolverEngine.cs   # CP-SAT execution & solution extraction
│       │
│       ├── Controllers/
│       │   └── TimetableController.cs     # 9 REST API endpoints
│       │
│       ├── Program.cs                     # Startup (Web API mode + CLI runner)
│       ├── TimetableSolver.csproj         # Google.OrTools, Swashbuckle.AspNetCore
│       └── TimetableSolver.http           # Visual Studio / VS Code REST Client test file
│
├── tests/
│   └── TimetableSolver.Tests/
│       ├── TimetableSolverTests.cs        # 5 automated integration & solver tests
│       └── TimetableSolver.Tests.csproj
│
├── full_school_timetable_output.json       # Master generated timetable output (24,580 lines)
├── test_endpoints.ps1                     # PowerShell automated verification script
├── README.md                              # Complete documentation
└── TimetableSolver.slnx                   # Solution file
```

---

## 4. Bundled Dataset (100% Self-Contained)

All datasets are packaged inside `src/TimetableSolver/Datasets/` and compiled as `<EmbeddedResource>` + `<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>`.

- **No absolute paths required:** The application runs identically in Visual Studio, `dotnet run`, Docker, CI/CD pipelines, or any other directory.
- **Embedded fallback:** If a file is missing on disk, `JsonDataLoader` automatically reads from assembly embedded resources.

---

## 5. Constraint Model & Production Rule Mapping

The constraint model maps strictly to the authoritative specification in `Timetable-Engine-Rules.md`:

| Rule ID | Rule Name | Category | CP-SAT Mathematical Formulation |
| :--- | :--- | :--- | :--- |
| **H1** | No Teacher Double-Booking | Hard | For each regular teacher $t$, day $d$, period $p$:<br>$$\sum_{a \in A(t)} X_{a, d, p} \le 1$$ |
| **H3** | Slot Uniqueness | Hard | For each section $s$, day $d$, period $p$:<br>$$\sum_{a \in A(s)} X_{a, d, p} = 1$$ |
| **H4** | No Lessons in Breaks | Hard | Break periods (Short Break 1, Lunch, Short Break 2) excluded from decision variables. |
| **H6** | Day Restrictions (`ONLY ON`) | Hard | $X_{a, d, p} = 0$ for all $d \notin \text{AllowedDays}(a)$. |
| **H7** | Daily Subject Max | Hard | For each assignment $a$ and day $d$:<br>$$\sum_{p \in P(d)} X_{a, d, p} \le \text{MaxPeriodsPerDay}(a)$$ |
| **H8** | Weekly Curriculum Quota | Hard | For each assignment $a$:<br>$$\sum_{d \in D} \sum_{p \in P(d)} X_{a, d, p} = \text{PeriodsPerWeek}(a) \quad (\text{Total } = 52)$$ |
| **H9** | Activity Fixed Slots | Hard | Fixed $(day, period)$ pinned to 1: $X_{a, d, p} = 1$. |
| **H10 / DATA-1** | Zero-Workload Isolation | Hard | $0$-workload and placeholder rows excluded from decision variables; reported in `dataConflicts[]`. |
| **G1** | Games/Library Morning Ban | Hard | For Games/Library assignments on all days:<br>$$X_{a, d, 1} = 0 \quad \text{and} \quad X_{a, d, 2} = 0$$ |
| **G2** | Senior Games + Library Ban | Hard | For Class 11 & 12 sections on day $d$:<br>$$\text{HasGames}_{s, d} + \text{HasLibrary}_{s, d} \le 1$$ |
| **L1** | Same First-Word Subject Ban | Hard | Subjects sharing first word (e.g. English Lang + Lit) cannot share the same day if days fit in week:<br>$$\sum_{a \in \text{Group}} \text{DayActive}_{a, d} \le 1$$ |
| **L2** | No Break-Crossing Blocks | Hard | Subject cannot occupy the period right before a break and right after the same break:<br>$$X_{a, d, p_{\text{before}}} + X_{a, d, p_{\text{after}}} \le 1$$ |
| **L3** | Max 3 Consecutive Slots | Hard | For any 4 consecutive slots in a day:<br>$$\sum_{k=p}^{p+3} X_{a, d, k} \le 3$$ |
| **H5, B1** | Block Subject Pairing | Hard | For block subjects ($\text{PPD} = 2$), two periods in a day must occupy an adjacent pair: $(1,2), (3,4), (5,6), (7,8), (9,10)$. |
| **T6, AC8** | Whole-School Activities | Rule | Activity instructors (Games, Dance, Karate, Music, etc.) run concurrently across sections without clash penalty. |
| **PR-MATH** | Math Morning Preference | Soft | Penalty weight ($+10$) added to objective for Math placed in afternoon periods (periods 5–8 on Mon–Thu). |
| **DB-RULE** | Activity Distribution | Soft | Penalty weight ($+8$) if Games/Library appears $>1$ time/day in Class 1–10 sections. |

---

## 6. Prerequisites & Setup

### Prerequisites
- **.NET SDK 8.0** ([Download .NET 8](https://dotnet.microsoft.com/download/dotnet/8.0))
- Windows, Linux, or macOS

### Clone & Build
```bash
# Clone the repository and navigate to the directory
cd "d:/Abhishek Gujre/Dotnet Task/Dotnet Task"

# Restore NuGet dependencies
dotnet restore

# Build solution
dotnet build
```

---

## 7. How to Run

### A. Web API & Swagger UI (Recommended)

Start the REST API server:

```bash
dotnet run --project src/TimetableSolver
```

- **Interactive Swagger UI:** 👉 [http://localhost:5000/swagger](http://localhost:5000/swagger)
- **Base API URL:** `http://localhost:5000/api/timetable`

---

### B. CLI Mode

To run the solver directly in console and export the master output JSON:

```bash
dotnet run --project src/TimetableSolver -- --cli
```

---

### C. Automated Test Suite

Execute the xUnit test suite (validates data loaders, constraints, sample school solve, and full school solve):

```bash
dotnet test
```

**Test Summary:**
```text
Passed!  - Failed: 0, Passed: 5, Skipped: 0, Total: 5 (100% Success)
  ✓ Test_EmbeddedDataset_LoadsDirectlyWithoutFilePath
  ✓ Test1_BellSchedule_And_Sections_LoadedCorrectly
  ✓ Test2_MarkdownParser_ClassWiseSubjects_And_TeacherAssignments
  ✓ Test3_Solve_SampleSchool_FindsFeasibleSolution
  ✓ Test4_Solve_FullSchool_GeneratesAll41Sections
```

---

### D. Endpoint Verification Script

Run the automated PowerShell verification script that tests all 9 endpoints:

```powershell
powershell -ExecutionPolicy Bypass -File .\test_endpoints.ps1
```

---

## 8. REST API Reference & Examples

### 1. `POST /api/timetable/generate`
Generates or retrieves the complete school timetable.

- **Fast / Instant Mode (Default):** Returns pre-generated full school timetable in **< 260ms**.
- **Live Recompute Mode:** Runs live Google OR-Tools CP-SAT solver.
- **Section Query:** Filter down to a specific section.

**Query Parameters:**
| Parameter | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `forceRecompute` | `bool` | `false` | Set `true` to force live CP-SAT optimization. |
| `timeoutSeconds` | `double` | `90` | Solver time limit in seconds (when `forceRecompute=true`). |
| `sectionId` | `string?` | `null` | Optional section slug/name (e.g. `10-lily`, `Grade 10 - Lily`). |

**cURL Examples:**
```bash
# Instant generate full school (Default)
curl -X POST "http://localhost:5000/api/timetable/generate"

# Generate specific section
curl -X POST "http://localhost:5000/api/timetable/generate?sectionId=10-lily"

# Force live recompute with 90s solver limit
curl -X POST "http://localhost:5000/api/timetable/generate?forceRecompute=true&timeoutSeconds=90"
```

---

### 2. `GET /api/timetable/summary`
Returns solver metadata, execution time, and summary metrics.

**Response Example:**
```json
{
  "success": true,
  "solver": {
    "engine": "Google OR-Tools CP-SAT",
    "status": "FEASIBLE",
    "wallTimeMs": 93240,
    "timeLimitSeconds": 90,
    "objectiveValue": 320
  },
  "summary": {
    "sectionsTotal": 41,
    "sectionsScheduled": 41,
    "totalSlotsScheduled": 2132,
    "teachersInvolved": 33,
    "unassignedPlaceholderRows": 92,
    "zeroWorkloadRows": 74
  },
  "dataConflictsCount": 74,
  "schedulingConflictsCount": 0
}
```

---

### 3. `GET /api/timetable/sections` & `GET /api/timetable/sections/{id}`
- `GET /api/timetable/sections` — Returns all 41 section weekly schedules.
- `GET /api/timetable/sections/{id}` — Returns schedule for a specific section by slug (`10-lily`), ID (`section-10-lily`), or display name (`Grade 10 - Lily`).

**cURL Example:**
```bash
curl -X GET "http://localhost:5000/api/timetable/sections/10-lily"
```

**Response Snippet:**
```json
{
  "section": "Grade 10 - Lily",
  "schedule": {
    "Monday": [
      {
        "period": 1,
        "slot": "08:00 - 08:45",
        "subject": "Mathematics",
        "teacherCode": "100021",
        "teacherName": "Ashish Thakur",
        "isActivity": false
      },
      {
        "period": 2,
        "slot": "08:45 - 09:30",
        "subject": "Science",
        "teacherCode": "100015",
        "teacherName": "Prashant Rai",
        "isActivity": false
      }
    ]
  }
}
```

---

### 4. `GET /api/timetable/teachers` & `GET /api/timetable/teachers/{id}`
- `GET /api/timetable/teachers` — Returns combined weekly schedules for all teachers.
- `GET /api/timetable/teachers/{id}` — Returns schedule for a specific teacher by code (`100047`) or name (`Ritika`).

**cURL Example:**
```bash
curl -X GET "http://localhost:5000/api/timetable/teachers/100047"
```

---

### 5. `GET /api/timetable/conflicts`
Returns data gaps (74 zero-workload records) and scheduling conflicts (0 double bookings).

**cURL Example:**
```bash
curl -X GET "http://localhost:5000/api/timetable/conflicts"
```

---

### 6. `GET /api/timetable/dataset` & `POST /api/timetable/load-data`
- `GET /api/timetable/dataset` — Inspect raw normalized entities (sections, teachers, curriculum rules, bell schedule).
- `POST /api/timetable/load-data` — Validates dataset and surfaces data conflicts.

---

## 9. Solver Benchmark & Results

Benchmarked on Windows x64:

```text
===============================================================================
  FULL SCHOOL CONSTRAINT-BASED TIMETABLE SOLVER (GOOGLE OR-TOOLS CP-SAT)
===============================================================================

[1/4] Loading and normalizing dataset...
      - Sections Loaded: 41
      - Teachers Loaded: 44
      - Weekly Capacity per section: 52 periods
      - Active Teaching Assignments: 446
      - UNASSIGNED-TT Placeholder Rows: 92 (Excluded from solver)
      - Zero-Workload Rows: 74 (Reported in conflicts)

[2/4] Building CP-SAT Constraint Model...
      - Boolean Decision Variables: X[assignment, day, period]
      - Enforcing Hard Constraints (H1-H10, G1-G2, L1-L3, B1, T6, DATA-1)
      - Enforcing Soft Preferences (PR-MATH, DB-RULE)

[3/4] Solving with Google OR-Tools CP-SAT...

[4/4] Solver Run Completed!
      - Solver Status: FEASIBLE
      - Objective Value: 320
      - Sections Scheduled: 41 / 41 (100%)
      - Total Teaching Slots Scheduled: 2,132 / 2,132 (100%)
      - Teacher Double-Booking Clashes: 0
      - Output File: full_school_timetable_output.json (665 KB)
```

---

## 10. Assumptions & Design Decisions

1. **Self-Contained Embedded Resources:** Datasets are embedded into the project binary to eliminate external relative path bugs across different running environments.
2. **Instant Cached Response with Live Fallback:** Web APIs should be responsive. `POST /api/timetable/generate` returns the pre-computed verified timetable instantly (<260ms) while preserving full capability to run a live solver solve via `forceRecompute=true`.
3. **Whole-School Activity Instructors:** Consistent with production engine rules `T6` and `AC8`, activity instructors (Games, Dance, Music, Karate, etc.) teach concurrent whole-school cohorts without triggering teacher clash penalties.
4. **Room Infrastructure:** With 41 sections and only 9 generic rooms in the raw dataset, sections are scheduled in their home classrooms, decoupling room allocation.
5. **Teacher Availability:** All active teachers are treated as available across all 6 working days unless explicit off-days are specified.

---

*Authored for the RDPL Timetable Product Developer Assessment.*
