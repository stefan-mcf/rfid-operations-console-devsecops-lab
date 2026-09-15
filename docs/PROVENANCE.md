# Source and build record

The RFID Operations Console was developed for SIT223 7.3HD as a separate implementation of RFID access decisions, local SQLite storage and queued delivery. It contains no customer code, data or deployment configuration.

This public repository starts with a single publication commit. Jenkins build 14 ran before publication against revision `a1152b51f98989d0e4052b4e3f677a9c561cb47e`. That revision remains in the private development archive rather than this repository's commit history.

The application, tests, Jenkinsfile, stage scripts and infrastructure configuration are byte-for-byte identical to that revision. [build-14-source.json](build-14-source.json) lists their original Git blob IDs and SHA-256 checksums. Documentation was revised for publication. Build 14's [raw outputs and screenshots](evidence/build-14/) retain their original contents and revision identifiers; they are not a new run against the publication commit.

The [7:20 demonstration](https://drive.google.com/file/d/1IiOHtabuf0zDHN0I_kxpE-9BG_8kJgIL/view?usp=drivesdk) shows build 14 and includes an earlier clone demonstration. The repository URL is unchanged and clones the published snapshot.
