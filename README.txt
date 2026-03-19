# Devian

## Overview

Devian is a framework designed to maximize development productivity by enforcing structure.

It is not a tool that helps you "build better code".
It is a system that ensures you **cannot build it wrong**.

> Think less. Build faster. Stay consistent.

---

## Philosophy

> **"Do not think about structure. Just build the game."**

Devian removes unnecessary decisions from development.
Instead of flexibility, it provides:

* consistency
* predictability
* scalability

---

## Core Principles

### 1. Structure over Freedom

All code must exist within a predefined structure.

* Domain / Protocol / System separation is mandatory
* Naming conventions are enforced
* Folder structure is fixed

Freedom is reduced to eliminate inconsistency and errors.

---

### 2. Single Source of Truth (SSOT)

Each piece of data must be defined in only one place.

* Tables define data
* Protocols define communication
* Code is generated from definitions

Duplicated definitions are not allowed.

---

### 3. Generate, Don't Write

Repetitive code should never be written manually.

* Tables → code generation
* Protocols → API generation
* Data → Actor/UI connection

The developer focuses only on essential logic.

---

### 4. Connect, Don't Contain

Each layer connects systems instead of owning logic.

* UI → display and connection only
* Panel → flow control
* System → business logic
* Data → definitions only

Responsibilities must not overlap.

---

### 5. Reduce Thinking

Developers should not make unnecessary decisions.

* One correct pattern
* One consistent structure
* No ambiguity

The framework guides development automatically.

---

## Identity

> Devian reduces developer freedom to maximize productivity and stability.

It enforces rules so that:

* structure is always correct
* code is always consistent
* systems are always aligned

---

## UI Architecture (Simplified)

```
UIManager
 └─ Canvas<>
     ├─ MainCanvas
     ├─ PopupCanvas
     └─ OverlayCanvas

Panel
 └─ Container
     └─ Frame + Plugin
```

### Key Concepts

* **Panel**: controls flow and state
* **Frame**: holds UI references
* **Plugin**: connects UI with systems (no logic)

---

## Popup & Toast

### Popup

* Blocking UI
* Requires user interaction
* Managed by stack

```
PopupCanvas
 ├─ Dim
 └─ PopupRoot
```

### Toast

* Non-blocking notification
* Auto-dismiss
* Does not interrupt user flow

```
OverlayCanvas
 └─ ToastRoot
```

---

## Development Flow

```
1. Define data (Table / Protocol)
2. Generate code
3. Connect UI (Plugin)
4. Control flow (Panel)
5. Build & Release
```

---

## Target Users

* Solo developers
* Small teams
* Developers using AI-assisted workflows
* Teams prioritizing speed and consistency

---

## Not for

* Developers who prefer full architectural freedom
* Experimental or constantly changing structures
* Highly customized UI frameworks

---

## Summary

Devian is not about writing better code.

It is about:

> **eliminating mistakes by removing unnecessary decisions.**

---

## One Line

> **"If you follow Devian, your structure is always correct."**
