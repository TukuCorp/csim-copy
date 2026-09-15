# Availability of the ETP/UNOPS Vietnam ETS TA deliverables

Date: 2026-09-13
Question: can the Task 5/6 deliverable and the per-session simulation results (system reports, auction tables, leaderboards) from the six Vietnam training sessions be obtained anywhere?

## Answer in one line
No. The three Training Reports that hold the session-level material were "submitted separately" to ETP and are not published anywhere; the Task 5/6 deliverable was tool access, not a report. Everything else in the TA is public and has now been collected.

## What ETP publishes for this TA (project page, verified)
`https://www.energytransitionpartnership.org/projects/emission-trading-system-piloting-and-simulation/` lists exactly seven documents: Inception Report (EN, VN), Final Report (EN, VN), Project Slide Deck (EN, VN), Terms of Reference. The user's `background/` Final Report was downloaded from here (same file name).

## What ETP's media library holds beyond the project page (found via the WordPress media API)
| File | What it is | Saved |
|---|---|---|
| `20231002_D1_Inception-Report-final.pdf` | Inception report; §3.5 has the CarbonSim spec table and the planned Vietnam adjustments | `research/carbonsim-sources/etp-official/` |
| `2020327_D3-Report-final_VN.pdf` | Task 3 readiness and awareness survey (237 companies); Vietnamese only | same |
| `2020327_D4-Report-final.pdf` (+VN) | Task 4 stakeholder mapping and engagement plan | same |
| `20250317_ETS_Task-9_Technical-Report_EN_clean.pdf` (+VN) | Task 9 technical report, Feb 2025, 57 pp: training design, CarbonSim description, tutor training, achievements, survey impact, lessons, and an annex giving the Training Reports' outline | same |
| `20250418_Presentation_EN.pptx.pdf` | Final policy-recommendation slide deck; no simulation numbers | same |
| `VIE_Emission-Trading-System-Piloting-and-Simulation-1.pdf` | Terms of Reference | same |
| `Press-Release-_Feb24_Training-courses...pdf` | Press release for Training 1 | not saved |

## What is not public
- **Training Reports 1–3** (Task 7 deliverables, one per pair of sessions). The Task 9 annex prints their outline: they contain a "Simulation section" for each day, lessons learned, survey results, agendas, materials, and participant lists. Whether they include CarbonSim system reports or leaderboards is not stated; the Dominican Republic report by the same trainer did include such tables, so it is plausible.
- **Milestone 2 package** (Tasks 3, 4, 5/6, submitted 2 Jan 2024). Tasks 3 and 4 are public as D3 and D4. The Task 5/6 deliverable was "a ready-to-use CarbonSim tool tailored for Vietnam" plus the web platform, with an optional executive summary; no standalone report exists to find.
- The web platform itself (vncarbonmarket.com) is lost to a domain hijack; the Wayback Machine holds its pages and PDFs.
- Raw CarbonSim data (auction tables, trade logs, leaderboards) from the six sessions would sit in EDF's sim3 database and in whatever exports the trainer took; nothing of that kind was published.

## Who to ask, and for what
1. **ETP/UNOPS (Bangkok, etp@unops.org; John Robert Cotton was the programme manager)**: the three Training Reports. They are UNOPS deliverables with a public-facing executive-summary requirement, so a release request is reasonable.
2. **VNEEC (Dang Hong Hanh, team leader)**: the same reports plus any CarbonSim system-report screenshots or exports kept for the debriefs; the Vietnamese parameter sheet used in each session.
3. **Josh Margolis**: session exports from the sim3 instance (system totals, auction results, leaderboards), which only the administrator can pull.

## Exhaustive search log (same day, second pass)
Hosts and methods checked, all negative for the Training Reports:
- ETP WordPress media API enumerated in full: 2,725 media items, 984 documents, every filename reviewed. Vietnam ETS TA files present: D1 inception (EN/VN), D3 (VN), D4 (EN/VN), Task 9 technical report (EN/VN), Final Report (EN/VN), final presentation (EN/VN), TOR, one press release. No file named or dated as a training report; nothing uploaded around the 11 Feb 2025 submission date except unrelated items.
- ETP publications catalogue (342 entries, 213 Vietnam), knowledge library, project page, semi-annual and annual reports, and the December 2024 independent evaluation (85 pp): the evaluation only says deliverables sit in "ETP's centralized document repository", which is internal.
- Wayback Machine CDX indexes of energytransitionpartnership.org uploads, vncarbonmarket.com uploads (22 documents, all lecture decks and glossaries) and eec.vn.
- Web search in English and Vietnamese ("báo cáo khóa đào tạo", "báo cáo kết quả", CarbonSim, VNEEC, ETP), Scribd/SlideShare/Studocu, gov.vn domains, UNOPS domains, and the trainer's name with results keywords.
- Vietnam ministry (MAE) summary of the 6 March 2025 closing workshop: narrative only.
- VNEEC's site (eec.vn): news posts only, no publications section for this TA.
- ICAP-hosted VNEEC presentation "Vietnam's carbon market development": mentions the simulation as a "good example", no results.

Side finding: since December 2024 a separate commercial programme, "Chương trình đào tạo mô phỏng thị trường carbon", has been run in Vietnam by IDEAS, VnEconomy, ISPONRE, EGP and "CarbonSim" with Josh Margolis, backed by VietinBank and Dragon Capital. Its module 6 ends with "reporting CarbonSim trading results", so session-level results are being generated outside the ETP TA as well; contact media@vneconomy.vn. This also shows CarbonSim is being commercialised in Vietnam, which is relevant to positioning a clone.

## Why this matters for the clone
The Dominican Republic report shows what a CarbonSim exercise report looks like when results are included: per-auction clearing prices by vintage, system totals, leaderboard. If the Vietnam Training Reports follow the same trainer's habit, they would give six calibration runs with roughly 650 human players on Vietnam parameters. Until then, calibration relies on the Dominican, Mexico and demo-video figures already harvested.
