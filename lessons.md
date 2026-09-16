# lessons

Corrections from the user, and the rule that stops each one recurring.

## 1. Research material is a specification, never content to ship (2026-09-16)

**What happened.** The first scenario file was seeded with 37 company names copied verbatim out of
the harvested `sim3_home.html` login-page dropdown, on the reading that the build plan's phrase
"seeded with the 39 firm names" authorised using them. The user corrected it: nothing in the
scenario may be copied from carbonsim.org, because the original is proprietary and the clone is
built from public documentation and observed behaviour only.

**Why the reading was wrong.** "Seeded with the 39 firm names" was a statement about the *shape*
and *size* of the dataset (39 participating companies of the same kinds: thermal plants, power and
gas utilities, cement, steel), not permission to lift the strings. `research/` is a specification
reference, and the working rules already said not to paste EDF's wording; the same logic applies to
any string that ships, not only UI copy.

**Rule.** Anything that ends up in a shipped artifact — names, wording, parameters presented as
data, sample values — must be written for this project. `research/` may be read to learn structure,
counts, ranges, field names and mechanics; it may never supply the literal value. When a plan item
names a source ("firm names from the login page"), treat it as "the same kind and count", then
author the content, and say in the file's own notes that it is original fiction. Scenario data files
carry a `notes` field for exactly this: state what is derived from documentation and what is
invented.
