# Feature: Precise package name for the delivery checkpoint and a consumer package README

Status: done
Created: 2026-11-01T17:09:37Z
Owner: implementation agent (SharpClaw host integration writer)
Origin: owner task "find a more precise name than Delivery" (2026-11-01T16:19:35Z); consumer-neutral audit of the shipped 0.2.0-alpha.6
packages (2026-11-01).

## Problem

1. The package id `Monstertacle.Multisession.Delivery` promises the send/runner module that ADR 0007 rejected. The package holds custody
   state only: an opaque durable checkpoint for one channel to a non-idempotent destination with atomic receipt and compare-and-swap
   grants. The established term in spec 009, ADR 0007, docs/delivery.md and the host is "delivery checkpoint".
2. Every 0.2.0-alpha.6 package ships the repository README, which contains repository development workflow and agent tooling
   instructions. A package README is consumer documentation.

## Requirements

- FR-001: Package id, project, assembly and namespace are `Monstertacle.Multisession.DeliveryCheckpoints`. Public type names, behavior and
  the on-disk format are unchanged. The package tags no longer contain `outbox`. No alias package, type forwarder or compatibility shim.
- FR-002: Every package packs `docs/package-readme.md` as its README; eng/Test-PackageLayout.ps1 refuses a package whose README differs
  from that file and refuses a package README that does not name the package version.
- FR-003: All eight packages advance together to 0.2.0-alpha.7. 0.2.0-alpha.6 bytes stay immutable and are not reissued.

## Success criteria

- Required CI (quality Linux/Windows with the 87 % gate, canonical consumers, Codemetr guard) passes on the final head.
- The independent package-only consumer (eng/Test-DeliveryConsumer.ps1) passes against the renamed package.
- The layout guard turns red when the repository README is packed (mutation probe).
