# MindCare — API Contract

> **Version:** 1.0  
> **Base URL:** `https://localhost:7010` (dev) / `https://api.mindcare.example` (prod)  
> **Auth:** Cookie-based session (ASP.NET Core Identity). Every mutating request **must** include the anti-forgery token in the `RequestVerificationToken` form field or the `X-CSRF-TOKEN` / `RequestVerificationToken` header.  
> **Content-Type (requests):** `application/json` unless marked as `multipart/form-data` or `application/x-www-form-urlencoded`.  
> **Content-Type (responses):** `application/json`.  
> **Roles:** `Parent` | `Child` — enforced server-side. Requests from the wrong role return `403 Forbidden`.

---

## Table of Contents

1. [Auth — Account](#1-auth--account)
   - [POST /Account/Register](#11-post-accountregister)
   - [POST /Account/Login](#12-post-accountlogin)
   - [POST /Account/Logout](#13-post-accountlogout)
2. [Parent — Child Management](#2-parent--child-management)
   - [GET /Parent/Index](#21-get-parentindex)
   - [POST /Parent/AddChild](#22-post-parentaddchild)
3. [Child — Dashboard & Profile](#3-child--dashboard--profile)
   - [GET /Child/Index](#31-get-childindex)
4. [Mood Check-In](#4-mood-check-in)
   - [POST /api/mood/checkin](#41-post-apimoodcheckin)
   - [GET /api/mood/today](#42-get-apimoodtoday)
5. [Journal](#5-journal)
   - [POST /api/journal/submit](#51-post-apijournalsubmit)
   - [GET /api/journal/history](#52-get-apijournalhistory)
6. [Wellbeing Assessment](#6-wellbeing-assessment)
   - [POST /api/assessment/submit](#61-post-apiassessmentsubmit)
   - [GET /api/assessment/history](#62-get-apiassessmenthistory)
   - [GET /api/assessment/{id}/answers](#63-get-apiassessmentidanswers)
7. [AI Report](#7-ai-report)
   - [GET /api/report/{assessmentId}](#71-get-apireportassessmentid)
8. [MindCare Buddy (Chat)](#8-mindcare-buddy-chat)
   - [POST /Chat/SendMessage](#81-post-chatsendmessage)
   - [GET /api/chat/history/{conversationId}](#82-get-apichathistoryconversationid)
9. [Parent — Child Activity Views](#9-parent--child-activity-views)
   - [GET /api/parent/child/{childProfileId}/daily-activity](#91-get-apiparentchildchildprofileddaily-activity)
   - [GET /api/parent/child/{childProfileId}/journal](#92-get-apiparentchildchildprofileidjournal)
   - [GET /api/parent/child/{childProfileId}/assessment/{assessmentId}/answers](#93-get-apiparentchildchildprofileidassessmentassessmentidanswers)
10. [Common Error Responses](#10-common-error-responses)
11. [Data Models Reference](#11-data-models-reference)
12. [Assessment Questions Reference](#12-assessment-questions-reference)

---

## 1. Auth — Account

### 1.1 `POST /Account/Register`

Registers a new **Parent** account and signs the user in.

**Auth required:** No  
**Role assigned on success:** `Parent`

#### Request body (`application/json`)

```json
{
  "fullName": "Jane Doe",
  "email": "jane@example.com",
  "password": "SecurePass1!",
  "confirmPassword": "SecurePass1!"
}
```

| Field           | Type   | Required | Constraints                        |
|-----------------|--------|----------|------------------------------------|
| `fullName`      | string | ✅       | 2–200 characters                   |
| `email`         | string | ✅       | Valid email, max 256 chars, unique  |
| `password`      | string | ✅       | 8–100 characters                   |
| `confirmPassword` | string | ✅   | Must match `password`              |

#### Success Response `302 Found`

Redirects to `GET /Parent/Index`.

#### Error Response `200 OK` (validation failure)

```json
{
  "success": false,
  "errors": {
    "": ["Email 'jane@example.com' is already taken."],
    "fullName": ["The FullName field is required."]
  }
}
```

> **Note:** Currently returns a Razor view. When migrating to a pure API, return `400 Bad Request` with the errors object above.

---

### 1.2 `POST /Account/Login`

Authenticates an existing user (Parent or Child) and sets the session cookie.

**Auth required:** No

#### Request body (`application/json`)

```json
{
  "userNameOrEmail": "jane@example.com",
  "password": "SecurePass1!",
  "rememberMe": false
}
```

| Field             | Type    | Required | Constraints                        |
|-------------------|---------|----------|------------------------------------|
| `userNameOrEmail` | string  | ✅       | Email or username, max 256 chars   |
| `password`        | string  | ✅       | —                                  |
| `rememberMe`      | boolean | ❌       | Defaults to `false`                |

#### Success Response `302 Found`

- If role is `Parent` → redirect to `GET /Parent/Index`
- If role is `Child` → redirect to `GET /Child/Index`
- If `returnUrl` query param is provided and is a local URL → redirect there

#### Error Response `400 Bad Request`

```json
{
  "success": false,
  "errors": {
    "": ["Invalid login attempt."]
  }
}
```

> **Lockout:** After repeated failures, the account is locked out temporarily (ASP.NET Identity default: 5 attempts → 5 min lockout).

---

### 1.3 `POST /Account/Logout`

Ends the authenticated session.

**Auth required:** Yes (any role)  
**Anti-forgery token:** Required

#### Request body

Empty / no body needed (POST with CSRF token only).

#### Success Response `302 Found`

Redirects to `GET /Home/Index`.

---

## 2. Parent — Child Management

### 2.1 `GET /Parent/Index`

Returns the parent's dashboard with their linked child profiles.

**Auth required:** Yes — `Parent` role

#### Success Response `200 OK`

```json
{
  "children": [
    {
      "id": 3,
      "childName": "Alex Smith",
      "dateOfBirth": "2014-03-10",
      "gender": "Male",
      "gradeOrAgeGroup": "Grade 7",
      "createdAt": "2026-10-01T08:00:00Z"
    }
  ]
}
```

| Field            | Type   | Description                            |
|------------------|--------|----------------------------------------|
| `id`             | int    | `ChildProfile.Id`                      |
| `childName`      | string | Child's display name                   |
| `dateOfBirth`    | string | ISO 8601 date `YYYY-MM-DD`             |
| `gender`         | string | nullable                               |
| `gradeOrAgeGroup`| string | nullable                               |
| `createdAt`      | string | ISO 8601 UTC timestamp                 |

---

### 2.2 `POST /Parent/AddChild`

Creates a linked child user account and `ChildProfile` record.

**Auth required:** Yes — `Parent` role  
**Anti-forgery token:** Required  
**Transaction:** Atomic — user creation + role assignment + profile row all succeed or all roll back.

#### Request body (`application/json`)

```json
{
  "childName": "Alex Smith",
  "dateOfBirth": "2014-03-10",
  "email": "alex@example.com",
  "password": "ChildPass1!",
  "confirmPassword": "ChildPass1!",
  "gender": "Male",
  "gradeOrAgeGroup": "Grade 7"
}
```

| Field            | Type   | Required | Constraints                                      |
|------------------|--------|----------|--------------------------------------------------|
| `childName`      | string | ✅       | 2–200 characters                                 |
| `dateOfBirth`    | string | ✅       | ISO date `YYYY-MM-DD`, must not be in the future |
| `email`          | string | ✅       | Valid email, max 256 chars, unique               |
| `password`       | string | ✅       | 8–100 characters                                 |
| `confirmPassword`| string | ✅       | Must match `password`                            |
| `gender`         | string | ❌       | Max 50 chars (e.g. `"Male"`, `"Female"`, `"Non-binary"`, `"Prefer not to say"`) |
| `gradeOrAgeGroup`| string | ❌       | Max 100 chars (e.g. `"Grade 7"`, `"Age 12"`)     |

#### Success Response `302 Found`

Redirects to `GET /Parent/Index`.

#### Error Response `400 Bad Request`

```json
{
  "success": false,
  "errors": {
    "dateOfBirth": ["Date of birth cannot be in the future."],
    "": ["Email 'alex@example.com' is already taken."]
  }
}
```

---

## 3. Child — Dashboard & Profile

### 3.1 `GET /Child/Index`

Returns the child's dashboard data — daily progress, mood count, journal status, last assessment date.

**Auth required:** Yes — `Child` role  
**Guard:** Server verifies `ChildProfile` exists for the authenticated user; returns `403` if not found.

#### Success Response `200 OK`

```json
{
  "childProfileId": 3,
  "childName": "Alex Smith",
  "todayMoodCount": 2,
  "journalSubmittedToday": false,
  "lastAssessmentDate": "2026-10-07",
  "dailyProgressCount": 1,
  "dailyProgressTotal": 2
}
```

| Field                   | Type    | Description                                      |
|-------------------------|---------|--------------------------------------------------|
| `childProfileId`        | int     | Used in all subsequent child API calls           |
| `childName`             | string  | Display name                                     |
| `todayMoodCount`        | int     | Number of mood check-ins done today              |
| `journalSubmittedToday` | boolean | Whether a journal entry exists for today's date  |
| `lastAssessmentDate`    | string  | ISO date of latest completed assessment, or null |
| `dailyProgressCount`    | int     | Activities completed today (mood + journal)      |
| `dailyProgressTotal`    | int     | Always `2`                                       |

---

## 4. Mood Check-In

### 4.1 `POST /api/mood/checkin`

Saves a mood check-in for the authenticated child. Multiple check-ins per day are allowed.

**Auth required:** Yes — `Child` role  
**Anti-forgery token:** Required

#### Request body (`application/json`)

```json
{
  "mood": "Great",
  "reason": "Had fun at art club"
}
```

| Field    | Type   | Required | Constraints                                                  |
|----------|--------|----------|--------------------------------------------------------------|
| `mood`   | string | ✅       | One of: `"Great"`, `"Good"`, `"Okay"`, `"Low"`, `"Worried"` |
| `reason` | string | ❌       | Max 300 characters; `null` if skipped                        |

#### Success Response `200 OK`

```json
{
  "id": 55,
  "mood": "Great",
  "reason": "Had fun at art club",
  "createdAt": "2026-11-18T14:30:00Z"
}
```

#### Error Response `400 Bad Request`

```json
{
  "success": false,
  "errors": {
    "mood": ["The mood field is required."]
  }
}
```

---

### 4.2 `GET /api/mood/today`

Returns all mood check-ins for the authenticated child for today (UTC date).

**Auth required:** Yes — `Child` role

#### Success Response `200 OK`

```json
{
  "date": "2026-11-18",
  "entries": [
    {
      "id": 54,
      "mood": "Good",
      "reason": "Feeling okay about school",
      "createdAt": "2026-11-18T03:42:00Z"
    },
    {
      "id": 55,
      "mood": "Great",
      "reason": "Had fun at art club",
      "createdAt": "2026-11-18T11:00:00Z"
    }
  ]
}
```

---

## 5. Journal

### 5.1 `POST /api/journal/submit`

Submits the child's daily journal entry. **One entry per child per calendar date** — a second submission on the same date must overwrite or be rejected (see note).

**Auth required:** Yes — `Child` role  
**Anti-forgery token:** Required

#### Request body (`application/json`)

```json
{
  "bestPart": "Had lunch with my friends.",
  "difficultPart": "PREFER_NOT_TO_ANSWER",
  "dayDescription": "Busy but okay.",
  "helpRequired": null,
  "emotion": "Calm"
}
```

| Field           | Type   | Required | Constraints / Values                                                                      |
|-----------------|--------|----------|-------------------------------------------------------------------------------------------|
| `bestPart`      | string | ✅       | Free text. Send `"PREFER_NOT_TO_ANSWER"` when child ticked the checkbox. Max 2000 chars.  |
| `difficultPart` | string | ✅       | Free text or `"PREFER_NOT_TO_ANSWER"`. Max 2000 chars.                                    |
| `dayDescription`| string | ✅       | Free text or `"PREFER_NOT_TO_ANSWER"`. Max 2000 chars.                                    |
| `helpRequired`  | string | ❌       | Free text, `"PREFER_NOT_TO_ANSWER"`, or `null` if left blank. Max 2000 chars.             |
| `emotion`       | string | ❌       | One of: `"Calm"`, `"Happy"`, `"Tired"`, `"Worried"` — or `null` if not selected.         |

> **Duplicate handling:** The `JournalEntries` table has a unique index on `(ChildProfileId, EntryDate)`. If the child already submitted today, the server should respond `409 Conflict` or upsert, depending on product decision.

#### Success Response `201 Created`

```json
{
  "id": 88,
  "entryDate": "2026-11-18",
  "createdAt": "2026-11-18T14:48:00Z"
}
```

#### Error Response `409 Conflict` (duplicate for today)

```json
{
  "success": false,
  "errors": {
    "entryDate": ["A journal entry for today already exists."]
  }
}
```

---

### 5.2 `GET /api/journal/history`

Returns the authenticated child's submitted journal entries, newest first.

**Auth required:** Yes — `Child` role

#### Query parameters

| Param  | Type | Default | Description          |
|--------|------|---------|----------------------|
| `page` | int  | `1`     | 1-based page number  |
| `size` | int  | `10`    | Items per page       |

#### Success Response `200 OK`

```json
{
  "totalCount": 14,
  "page": 1,
  "size": 10,
  "entries": [
    {
      "id": 88,
      "entryDate": "2026-11-18",
      "bestPart": "Had lunch with my friends.",
      "difficultPart": "PREFER_NOT_TO_ANSWER",
      "dayDescription": "Busy but okay.",
      "helpRequired": null,
      "emotion": "Calm",
      "createdAt": "2026-11-18T14:48:00Z"
    }
  ]
}
```

---

## 6. Wellbeing Assessment

### 6.1 `POST /api/assessment/submit`

Submits a completed 10-question wellbeing assessment for the authenticated child.

**Auth required:** Yes — `Child` role  
**Anti-forgery token:** Required

#### Request body (`application/json`)

```json
{
  "answers": [
    { "questionId": 1, "questionText": "Do you feel happy most of the time?", "answer": "Often" },
    { "questionId": 2, "questionText": "Do you feel able to talk about your feelings?", "answer": "Sometimes" },
    { "questionId": 3, "questionText": "Do you feel stressed about schoolwork?", "answer": "Sometimes" },
    { "questionId": 4, "questionText": "Do you feel able to focus when you study?", "answer": "Often" },
    { "questionId": 5, "questionText": "Do you feel comfortable talking to your friends?", "answer": "Always" },
    { "questionId": 6, "questionText": "Do you feel included by other children?", "answer": "Often" },
    { "questionId": 7, "questionText": "Do you get enough sleep?", "answer": "Sometimes" },
    { "questionId": 8, "questionText": "Do you make time for things you enjoy?", "answer": "Often" },
    { "questionId": 9, "questionText": "Do you feel confident about yourself?", "answer": "Often" },
    { "questionId": 10, "questionText": "Do you feel proud when you learn something new?", "answer": "Always" }
  ]
}
```

| Field                   | Type   | Required | Constraints                                                        |
|-------------------------|--------|----------|--------------------------------------------------------------------|
| `answers`               | array  | ✅       | Exactly **10** items                                               |
| `answers[].questionId`  | int    | ✅       | Integer 1–10 (unique within submission)                            |
| `answers[].questionText`| string | ✅       | The question label (stored verbatim for auditability)              |
| `answers[].answer`      | string | ✅       | One of: `"Never"`, `"Rarely"`, `"Sometimes"`, `"Often"`, `"Always"` |

#### Success Response `201 Created`

```json
{
  "assessmentId": 1042,
  "assessmentDate": "2026-11-18",
  "status": "Submitted",
  "createdAt": "2026-11-18T14:55:00Z"
}
```

After creation, the server should **asynchronously** trigger AI report generation (see §7).

#### Error Response `400 Bad Request`

```json
{
  "success": false,
  "errors": {
    "answers": ["Exactly 10 answers are required."],
    "answers[2].answer": ["Answer must be one of: Never, Rarely, Sometimes, Often, Always."]
  }
}
```

---

### 6.2 `GET /api/assessment/history`

Returns the authenticated child's assessment list, newest first.

**Auth required:** Yes — `Child` role

#### Query parameters

| Param  | Type | Default | Description |
|--------|------|---------|-------------|
| `page` | int  | `1`     | —           |
| `size` | int  | `10`    | —           |

#### Success Response `200 OK`

```json
{
  "totalCount": 4,
  "page": 1,
  "size": 10,
  "assessments": [
    {
      "id": 1042,
      "assessmentDate": "2026-10-07",
      "status": "Submitted",
      "hasReport": true,
      "createdAt": "2026-10-07T05:02:00Z"
    }
  ]
}
```

---

### 6.3 `GET /api/assessment/{id}/answers`

Returns the individual question-answer pairs for one assessment.

**Auth required:** Yes — `Child` or `Parent` (parent must own the child profile linked to the assessment)

#### Path parameters

| Param | Type | Description              |
|-------|------|--------------------------|
| `id`  | int  | `Assessment.Id`          |

#### Success Response `200 OK`

```json
{
  "assessmentId": 1042,
  "assessmentDate": "2026-10-07",
  "answers": [
    { "questionId": 1, "questionText": "Do you feel happy most of the time?", "answer": "Often" },
    { "questionId": 2, "questionText": "Do you feel able to talk about your feelings?", "answer": "Sometimes" }
  ]
}
```

#### Error Response `404 Not Found`

```json
{ "success": false, "errors": { "": ["Assessment not found."] } }
```

---

## 7. AI Report

### 7.1 `GET /api/report/{assessmentId}`

Returns the AI-generated wellbeing report for a submitted assessment.

**Auth required:** Yes — `Parent` role (must own the child profile linked to the assessment)

#### Path parameters

| Param          | Type | Description     |
|----------------|------|-----------------|
| `assessmentId` | int  | `Assessment.Id` |

#### Success Response `200 OK`

```json
{
  "id": 77,
  "assessmentId": 1042,
  "assessmentDate": "2026-10-07",
  "childName": "Alex Smith",
  "childAge": 12,
  "gradeOrAgeGroup": "Grade 7",
  "summary": "Alex's responses suggest a generally positive sense of self...",
  "strengths": "Comfortable building friendships\nCan recognize calming strategies\nEnjoys positive daily moments",
  "areasOfConcern": "Talk about schoolwork pressure\nProtect a consistent sleep routine",
  "suggestions": "Ask open questions\nMake space for breaks\nCelebrate effort",
  "categoryScores": {
    "emotionalWellbeing": 82,
    "academicStress": 58,
    "socialRelationships": 78,
    "sleepAndLifestyle": 70,
    "selfConfidence": 90
  },
  "overallPattern": "Positive",
  "generatedAt": "2026-10-07T05:10:00Z"
}
```

| Field              | Type   | Description                                                              |
|--------------------|--------|--------------------------------------------------------------------------|
| `summary`          | string | AI-generated plain-text summary paragraph                                |
| `strengths`        | string | Newline-separated bullet points                                          |
| `areasOfConcern`   | string | Newline-separated bullet points                                          |
| `suggestions`      | string | Newline-separated suggestions for parents                                |
| `categoryScores`   | object | Percentage scores 0–100 per category (qualitative, not clinical)         |
| `overallPattern`   | string | One of: `"Positive"`, `"Steady"`, `"Needs Attention"`                   |

#### Error Response `404 Not Found`

```json
{ "success": false, "errors": { "": ["Report not ready yet or not found."] } }
```

> **Note:** Report generation is asynchronous. The frontend should poll this endpoint or use a webhook/SignalR notification. Return `202 Accepted` with `{ "status": "Processing" }` while the report is being generated.

---

## 8. MindCare Buddy (Chat)

### 8.1 `POST /Chat/SendMessage`

Sends a message to the AI buddy and returns its reply. The endpoint is referenced in the view as `data-endpoint="/Chat/SendMessage"`.

**Auth required:** Yes — `Child` role  
**Anti-forgery token:** Required

#### Request body (`application/json`)

```json
{
  "message": "I am feeling stressed about school.",
  "conversationId": "conv-abc123"
}
```

| Field            | Type   | Required | Constraints                                                                             |
|------------------|--------|----------|-----------------------------------------------------------------------------------------|
| `message`        | string | ✅       | Max 500 characters                                                                      |
| `conversationId` | string | ❌       | UUID or opaque token for the ongoing conversation. If omitted, the server creates a new one. |

#### Success Response `200 OK`

```json
{
  "conversationId": "conv-abc123",
  "userMessage": {
    "id": 201,
    "sender": "child",
    "message": "I am feeling stressed about school.",
    "createdAt": "2026-11-18T15:00:00Z"
  },
  "buddyReply": {
    "id": 202,
    "sender": "buddy",
    "message": "That sounds tough. Would you like to tell me more about what's worrying you?",
    "createdAt": "2026-11-18T15:00:02Z"
  }
}
```

| Field                | Type   | Description                               |
|----------------------|--------|-------------------------------------------|
| `conversationId`     | string | Echo back or newly assigned               |
| `userMessage.sender` | string | Always `"child"`                          |
| `buddyReply.sender`  | string | Always `"buddy"`                          |

#### Error Response `400 Bad Request`

```json
{
  "success": false,
  "errors": { "message": ["Message cannot be empty."] }
}
```

---

### 8.2 `GET /api/chat/history/{conversationId}`

Loads the full message history for a conversation (used to restore a session on page reload).

**Auth required:** Yes — `Child` role

#### Path parameters

| Param            | Type   | Description          |
|------------------|--------|----------------------|
| `conversationId` | string | The conversation ID  |

#### Success Response `200 OK`

```json
{
  "conversationId": "conv-abc123",
  "messages": [
    {
      "id": 201,
      "sender": "child",
      "message": "I am feeling stressed about school.",
      "createdAt": "2026-11-18T15:00:00Z"
    },
    {
      "id": 202,
      "sender": "buddy",
      "message": "That sounds tough. Would you like to tell me more?",
      "createdAt": "2026-11-18T15:00:02Z"
    }
  ]
}
```

---

## 9. Parent — Child Activity Views

### 9.1 `GET /api/parent/child/{childProfileId}/daily-activity`

Returns a parent's view of a child's activity for a given date.

**Auth required:** Yes — `Parent` role (must own the child profile)

#### Path & query parameters

| Param            | Location | Type   | Required | Description                                |
|------------------|----------|--------|----------|--------------------------------------------|
| `childProfileId` | path     | int    | ✅       | `ChildProfile.Id`                          |
| `date`           | query    | string | ❌       | ISO date `YYYY-MM-DD`. Defaults to today.  |

#### Success Response `200 OK`

```json
{
  "date": "2026-11-18",
  "childName": "Alex Smith",
  "moodEntries": [
    {
      "id": 54,
      "mood": "Great",
      "reason": "Had fun at art club",
      "createdAt": "2026-11-18T02:55:00Z"
    }
  ],
  "journal": {
    "id": 88,
    "entryDate": "2026-11-18",
    "bestPart": "Finished my art project.",
    "difficultPart": "PREFER_NOT_TO_ANSWER",
    "dayDescription": "Busy but okay.",
    "helpRequired": null,
    "emotion": "Tired",
    "submittedAt": "2026-11-18T14:18:00Z"
  },
  "latestAssessment": {
    "id": 1042,
    "assessmentDate": "2026-10-07",
    "status": "Submitted",
    "hasReport": true
  }
}
```

> `journal` is `null` if no journal entry exists for the date.  
> `latestAssessment` is `null` if no assessments exist.

---

### 9.2 `GET /api/parent/child/{childProfileId}/journal`

Returns all journal entries for a child visible to the parent, newest first.

**Auth required:** Yes — `Parent` role

#### Path & query parameters

| Param            | Location | Type | Default | Description       |
|------------------|----------|------|---------|-------------------|
| `childProfileId` | path     | int  | —       | `ChildProfile.Id` |
| `page`           | query    | int  | `1`     | —                 |
| `size`           | query    | int  | `10`    | —                 |

#### Success Response `200 OK`

```json
{
  "totalCount": 14,
  "page": 1,
  "size": 10,
  "entries": [
    {
      "id": 88,
      "entryDate": "2026-11-18",
      "bestPart": "Had lunch with my friends.",
      "difficultPart": "PREFER_NOT_TO_ANSWER",
      "dayDescription": "Busy but okay.",
      "helpRequired": null,
      "emotion": "Calm",
      "createdAt": "2026-11-18T14:48:00Z"
    }
  ]
}
```

---

### 9.3 `GET /api/parent/child/{childProfileId}/assessment/{assessmentId}/answers`

Returns a child's answers to a specific assessment, for the parent.

**Auth required:** Yes — `Parent` role (must own the child profile)

#### Path parameters

| Param            | Type | Description              |
|------------------|------|--------------------------|
| `childProfileId` | int  | `ChildProfile.Id`        |
| `assessmentId`   | int  | `Assessment.Id`          |

#### Success Response `200 OK`

```json
{
  "assessmentId": 1042,
  "assessmentDate": "2026-10-07",
  "childName": "Alex Smith",
  "answers": [
    { "questionId": 1, "category": "Emotional well-being", "questionText": "Do you feel happy most of the time?", "answer": "Often" },
    { "questionId": 2, "category": "Emotional well-being", "questionText": "Do you feel able to talk about your feelings?", "answer": "Sometimes" },
    { "questionId": 3, "category": "Academic stress", "questionText": "Do you feel stressed about schoolwork?", "answer": "Sometimes" },
    { "questionId": 4, "category": "Academic stress", "questionText": "Do you feel able to focus when you study?", "answer": "Often" },
    { "questionId": 5, "category": "Social relationships", "questionText": "Do you feel comfortable talking to your friends?", "answer": "Always" },
    { "questionId": 6, "category": "Social relationships", "questionText": "Do you feel included by other children?", "answer": "Often" },
    { "questionId": 7, "category": "Sleep and lifestyle", "questionText": "Do you get enough sleep?", "answer": "Sometimes" },
    { "questionId": 8, "category": "Sleep and lifestyle", "questionText": "Do you make time for things you enjoy?", "answer": "Often" },
    { "questionId": 9, "category": "Self-confidence", "questionText": "Do you feel confident about yourself?", "answer": "Often" },
    { "questionId": 10, "category": "Self-confidence", "questionText": "Do you feel proud when you learn something new?", "answer": "Always" }
  ]
}
```

---

## 10. Common Error Responses

| HTTP Status | When                                              |
|-------------|---------------------------------------------------|
| `400`       | Validation failure — body contains `errors` map   |
| `401`       | Not authenticated — redirect to `/Account/Login`  |
| `403`       | Authenticated but wrong role, or resource doesn't belong to the caller |
| `404`       | Resource not found                                |
| `409`       | Duplicate (e.g. journal already submitted today)  |
| `500`       | Unhandled server error                            |

#### Standard error envelope

```json
{
  "success": false,
  "errors": {
    "<fieldName | empty string for general>": ["Error message."]
  }
}
```

---

## 11. Data Models Reference

### `ApplicationUser` (extends `IdentityUser`)

| Field       | Type     | Notes                        |
|-------------|----------|------------------------------|
| `Id`        | string   | GUID — ASP.NET Identity PK   |
| `FullName`  | string   | Required                     |
| `Email`     | string   | Also used as `UserName`      |
| `CreatedAt` | DateTime | UTC, server-assigned         |

### `ChildProfile`

| Field            | Type     | Notes                                       |
|------------------|----------|---------------------------------------------|
| `Id`             | int      | PK                                          |
| `ChildUserId`    | string   | FK → `AspNetUsers.Id`; unique               |
| `ParentUserId`   | string   | FK → `AspNetUsers.Id`; indexed              |
| `ChildName`      | string   | Required                                    |
| `DateOfBirth`    | DateOnly | Required                                    |
| `Gender`         | string?  | Optional                                    |
| `GradeOrAgeGroup`| string?  | Optional                                    |
| `CreatedAt`      | DateTime | UTC                                         |

### `MoodEntry`

| Field            | Type     | Notes                                                      |
|------------------|----------|------------------------------------------------------------|
| `Id`             | int      | PK                                                         |
| `ChildProfileId` | int      | FK → `ChildProfiles.Id`                                    |
| `Mood`           | string   | `"Great"` \| `"Good"` \| `"Okay"` \| `"Low"` \| `"Worried"` |
| `Reason`         | string?  | Optional explanation, max 300 chars                        |
| `CreatedAt`      | DateTime | UTC — multiple per day allowed                             |

### `JournalEntry`

| Field            | Type     | Notes                                                       |
|------------------|----------|-------------------------------------------------------------|
| `Id`             | int      | PK                                                          |
| `ChildProfileId` | int      | FK → `ChildProfiles.Id`                                     |
| `EntryDate`      | DateOnly | Unique per child per day (`UNIQUE(ChildProfileId, EntryDate)`) |
| `BestPart`       | string   | Required; `"PREFER_NOT_TO_ANSWER"` sentinel                 |
| `DifficultPart`  | string   | Required; `"PREFER_NOT_TO_ANSWER"` sentinel                 |
| `DayDescription` | string   | Required; `"PREFER_NOT_TO_ANSWER"` sentinel                 |
| `HelpRequired`   | string?  | Optional; `"PREFER_NOT_TO_ANSWER"` sentinel                 |
| `CreatedAt`      | DateTime | UTC                                                         |

> **Frontend note:** The `emotion` field displayed in the UI (Calm / Happy / Tired / Worried) is **not** currently in the `JournalEntry` model. The backend engineer should add an `Emotion` nullable string column, or the frontend stores it as a suffix in an existing field.

### `Assessment`

| Field            | Type     | Notes                                             |
|------------------|----------|---------------------------------------------------|
| `Id`             | int      | PK                                                |
| `ChildProfileId` | int      | FK → `ChildProfiles.Id`                           |
| `AssessmentDate` | DateOnly | Date of submission                                |
| `Status`         | string   | `"InProgress"` \| `"Submitted"`                   |
| `CreatedAt`      | DateTime | UTC                                               |

### `AssessmentAnswer`

| Field          | Type   | Notes                                                              |
|----------------|--------|--------------------------------------------------------------------|
| `Id`           | int    | PK                                                                 |
| `AssessmentId` | int    | FK → `Assessments.Id`                                              |
| `QuestionId`   | int    | 1–10 (unique per assessment via DB constraint)                     |
| `QuestionText` | string | Stored verbatim                                                    |
| `Answer`       | string | `"Never"` \| `"Rarely"` \| `"Sometimes"` \| `"Often"` \| `"Always"` |

### `AIReport`

| Field            | Type     | Notes                                      |
|------------------|----------|--------------------------------------------|
| `Id`             | int      | PK                                         |
| `AssessmentId`   | int      | FK → `Assessments.Id`; unique (1:1)        |
| `Summary`        | string   | AI-generated paragraph                     |
| `Strengths`      | string   | Newline-separated bullets                  |
| `AreasOfConcern` | string   | Newline-separated bullets                  |
| `Suggestions`    | string   | Newline-separated suggestions for parents  |
| `GeneratedAt`    | DateTime | UTC                                        |

### `ChatMessage`

| Field            | Type     | Notes                                         |
|------------------|----------|-----------------------------------------------|
| `Id`             | int      | PK                                            |
| `ChildProfileId` | int      | FK → `ChildProfiles.Id`                       |
| `ConversationId` | string   | Groups messages into a conversation thread    |
| `Sender`         | string   | `"child"` \| `"buddy"`                        |
| `Message`        | string   | Max 500 chars                                 |
| `CreatedAt`      | DateTime | UTC                                           |

---

## 12. Assessment Questions Reference

The 10 questions are fixed and grouped by category. The frontend sends `questionId` and `questionText` verbatim.

| ID | Category              | Question text                                      |
|----|-----------------------|----------------------------------------------------|
| 1  | Emotional well-being  | Do you feel happy most of the time?                |
| 2  | Emotional well-being  | Do you feel able to talk about your feelings?      |
| 3  | Academic stress       | Do you feel stressed about schoolwork?             |
| 4  | Academic stress       | Do you feel able to focus when you study?          |
| 5  | Social relationships  | Do you feel comfortable talking to your friends?   |
| 6  | Social relationships  | Do you feel included by other children?            |
| 7  | Sleep and lifestyle   | Do you get enough sleep?                           |
| 8  | Sleep and lifestyle   | Do you make time for things you enjoy?             |
| 9  | Self-confidence       | Do you feel confident about yourself?              |
| 10 | Self-confidence       | Do you feel proud when you learn something new?    |

**Valid answers for every question:** `"Never"` | `"Rarely"` | `"Sometimes"` | `"Often"` | `"Always"`

---

## Open Questions / Decisions for Backend Engineer

| # | Question | Impact |
|---|----------|--------|
| 1 | Should the **journal** `emotion` be a separate DB column or derived from free text? | Affects `JournalEntry` schema migration |
| 2 | Should **duplicate journal** submissions (same day) overwrite or be rejected with `409`? | Endpoint §5.1 behaviour |
| 3 | Is **AI report generation** synchronous or async (queue/background job)? | Polling vs. webhook/SignalR for §7 |
| 4 | Should the **chat endpoint** stream responses (Server-Sent Events / WebSocket) or be request-response? | Buddy UX latency |
| 5 | Does the parent see the child's **chat history**? Currently `ChatMessage` stores them but no parent API exposes them. | Privacy policy decision |
| 6 | **Anti-forgery tokens for SPA/API calls:** should the app switch to JWT Bearer + SameSite cookie, or stick with cookie auth + CSRF headers? | All POST endpoints |
| 7 | Should the parent be able to **select which child** to view when they have multiple children? Currently `childProfileId` is a path param — the UI needs a child selector. | §9.x endpoints |
