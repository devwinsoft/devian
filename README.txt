# Devian

> **Structured game development framework with code generation and unified systems**

Devian is a framework that provides a complete development environment for building games with consistency, speed, and scalability.

It combines:

* structured architecture
* code generation
* runtime systems
* tooling pipeline

into a single unified workflow.

---

## What Devian Provides

Devian is not just a library.
It is a **full development stack**.

### Core Components

* **Domain & Protocol System**

  * Structured data and communication definitions
  * Shared across runtime and tools

* **Builder & Code Generation**

  * Table → code
  * Protocol → API
  * Data → runtime assets

* **Runtime Systems**

  * Account
  * Save (Local / Cloud)
  * Purchase
  * Reward
  * Mission
  * Push / Message
  * Localization

* **Unity Runtime (UPM)**

  * UI system (Panel / Frame / Plugin)
  * Managers and core components
  * Mobile integration (Firebase, GPGS, etc.)

* **Tooling**

  * CLI / Builder
  * Data pipeline
  * Error reporting

---

## Architecture Overview

Devian enforces a layered structure across all systems.

### High-Level Layers

* **Data Layer**

  * Tables
  * Protocol definitions
  * SSOT (Single Source of Truth)

* **Build Layer**

  * Code generation
  * Asset generation
  * Validation

* **Runtime Layer**

  * Systems (Account, Reward, Purchase, etc.)
  * Platform integrations

* **UI Layer**

  * Panel (flow control)
  * Frame (UI references)
  * Plugin (connection layer)

---

## UI Structure

Devian UI is modular and composable.

* **UIManager** manages the entire UI system
* **Canvas** is separated by role (Main / Popup / Overlay)
* **Panel** controls screen logic
* **Container / Frame** define layout and references
* **Plugin** connects UI to systems (no business logic)

This allows UI to stay lightweight and reusable.

---

## Popup & Toast

### Popup

* Blocking UI
* Managed by stack
* Requires user interaction

### Toast

* Non-blocking notification
* Auto-dismiss
* Does not interrupt gameplay

---

## Development Flow

Devian development follows a fixed pipeline:

1. Define data (Table / Protocol)
2. Run builder (code & asset generation)
3. Implement systems (if needed)
4. Connect UI via Plugin
5. Control flow via Panel
6. Build & release

---

## Key Characteristics

### 1. Structured by Default

All components follow predefined conventions:

* Domain / Protocol separation
* Naming rules
* Folder structure

---

### 2. Code Generation Driven

Repetitive work is handled by the builder:

* No manual boilerplate
* Consistent API generation
* Reduced human error

---

### 3. System-Oriented Design

Features are implemented as independent systems:

* loosely coupled
* reusable
* platform-aware

---

### 4. Clear Responsibility Separation

* Data → definition only
* System → logic only
* UI → display and connection only

---

### 5. Scalable Workflow

Designed for:

* multiple projects
* long-term operation
* cross-platform expansion

---

## Who It's For

* Solo developers building full-stack games
* Small teams needing consistency
* Developers using AI-assisted workflows
* Projects requiring structured data pipelines

---

## Not For

* Projects requiring full architectural freedom
* Highly experimental or constantly changing structures
* Minimal prototypes without system needs

---

## Identity

Devian is a framework that prioritizes:

* consistency over flexibility
* automation over repetition
* structure over improvisation

---

## Summary

Devian provides a unified way to build games by combining:

* structured architecture
* code generation
* runtime systems
* UI composition

into a single workflow.

---

## One Line

> **Build games with structure, not guesswork.**
