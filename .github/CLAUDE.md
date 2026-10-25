# .github — workflows, scripts, release mechanics

The runbook (one-time settings, routines, the Dependabot procedure) is `docs/maintaining.md`; the reasoning is ADR 0003
(stable track), 0009 (previews) and 0004 (supply chain). This file holds what an edit here must not break.

## Previews (`preview.yml`)

- Runs Mondays at 07:15 Europe/Paris and on dispatch from `main`; the `nuget` environment deploys from `main` only.
- `scripts/preview-gate.sh` decides `publish`, `repair` or `none` train by train, against the version nuget.org has of
  each train from the nearest commit (its base), and fails the run rather than guess when a lookup fails; its header
  holds its inputs and outputs. Every train goes out at its own version, every package of the train at that version, or
  none (ADR 0012). The trains, their tag prefixes and breaking rules come from `release-trains.mjs trains`, the core
  first; the IDs from `package-ids.sh --trains` at `HEAD`, an older commit's mapped through that commit's own list
  (`release-trains.mjs --list <file> trains-of`, `HEAD`'s list for a commit from before it), as its pack read them. A
  version a package was packed at on another train is no base of its new train, and a pushed version below one is
  refused: ADR 0012 moves a package only once its new train's next version is above the package's last one.
- One commit is repaired per run: when IDs a train's base commit packed lack its base version (a push stopped
  midway), the gate repairs the base commit of the first such train, the core's first, and fails when another train
  misses IDs at another commit ("complete <train> <version> by hand"). Each train missing IDs there is pushed at its
  base version, which must be `X.Y.Z-preview.N`; every other train is packed there and never pushed, for the tests and
  `verify-packages.sh`: at its base version when that commit packed it, the core otherwise at the floor of the repaired
  packages' dependency on it (one floor, which nuget.org must have for every core ID), any other train at the core's
  version, a placeholder.
- A publish (a package input changed since any train's base, each train's own, since a revert can undo what lies
  between two bases) pushes every train but one nuget.org already has
  whole from `HEAD` at the version computed for it, which is packed again and not pushed. The `version` job runs
  third-party JavaScript, so the gate takes only each train's release type from it: a pushed train's version must be
  the one it recomputes from the train's last stable tag and the height since (`<prefix><last>..HEAD`), `major` is
  refused while the train's `breaking` is `minor`, any train but the core is held to the line rule, and the version
  must be new to nuget.org.
- The gate fails when nuget.org has a stable version of a train above the train's newest tag merged into `HEAD` ("a
  `<prefix><version>` tag is missing (a release without its twin tag?)"), since every version of the train would be
  computed from the wrong release; a package moved into a train behind a stable version it already has fails the same
  way. The remedy is in `docs/maintaining.md`.
- `scripts/preview-gate.test.mjs` replays the gate against a throwaway repository and a flat container `node:http`
  serves on `127.0.0.1` (curl answers `000` for a `file://` URL), and holds its decisions as they are recorded there:
  the one-version gate's, replayed with both trains twin-tagged at one version, then what deciding per train adds (a
  train whole at `HEAD`, one train or both partial, partial at two commits, a stable release of both, one version
  string packed from two commits, a missing twin tag, the line rule), each refusal of a decision, `--recheck` and
  `--report`. A failed lookup (an HTTP error, a nuspec naming no commit, a commit the clone lacks) is not replayed, nor
  is a `<PackageVersion>` change, which makes the gate restore. It needs `git`, `jq`, `curl`, `perl`, GNU `grep -P` and
  `node`, as the gate does. Its one seam, `PACKAGE_IDS_SCRIPT`, replaces `package-ids.sh` (dotnet) by a stub reading
  `src/ids.txt`, with `--trains` each ID on the train the tree's `src/release-trains.json` gives it, which the gate runs
  through `bash`: no workflow sets it, nor may, since the gate must read the IDs `package-ids.sh` prints, the set
  `preview.yml`'s pack job publishes. A change to the gate's decisions changes those recordings in the same pull request.
- The version: `scripts/next-version.mjs` runs semantic-release's commit analyzer with `.releaserc.json`, without a
  token, and suffixes `-preview.<commits since the last stable tag>` (`0.3.0-preview.172` leads to `0.3.0`; with no
  commit that releases anything, the next patch). It needs `fetch-depth: 0`.
- `next-version.mjs --trains` adds each train's version, from its own last stable tag and the commits of its range
  that release it, the line rule applied (ADR 0012), and writes a table to the job summary. `preview.yml`'s `version`
  job runs it, and the plan hands its `preview_versions` and `release_types` to the gate (`NEXT_VERSIONS`,
  `RELEASE_TYPES`): on that critical path, neither the job's `setup-dotnet` nor its restore takes `continue-on-error`.
  `release.yml`'s `pack` job runs it after the one version, in a `continue-on-error` step whose table nothing reads,
  or, on a per-train run, in its `Release plan` step, whose plan the release job reads, and `release tooling` runs it
  on a pull request, where it can fail the check. It reads the list and the restore
  graph of the tree checked out (`release-trains.mjs readModel`, `artifacts/obj/<project>/project.assets.json` after
  `dotnet restore src/AdCodicem.ValueObjects.Packages.slnf`), so it computes `HEAD` only, and fails without the
  restore, or without a stable tag of each train merged (`satellites-v0.2.1` on `68abdc7` is the satellites'
  baseline). Without `--trains` its output is today's six keys, which `release.yml` reads.
- MinVer receives it through `RELEASE_TRAIN_VERSIONS` (`core=X satellites=Y`, which `release-pack.sh` sets: each
  train's own version in a preview, every train at the one version in a stable release until `release.yml` versions
  them apart, ADR 0012), which `src/Directory.Build.props` turns into each project's `MinVerVersionOverride`. The
  build refuses `MINVERVERSIONOVERRIDE`, one version for every train, and a train the variable leaves out
  (`src/Directory.Build.targets`); `scripts/verify-packages.sh` refuses any version but `X.Y.Z-preview.N` unless
  `ALLOW_ANY_VERSION=true`.
- Order: the `suites` job runs every family's project of the unit suite, then the generator and RDG suites, on that
  exact commit; a job of its own packs every train of `src/`, pushed or not, at the version the plan gives it;
  `heavy.yml` runs on that commit too, its compat job installing the packed set by the artifact's id; the `publish`
  job, which needs all of them, rechecks the set (`verify-packages.sh`) and nuget.org (`preview-gate.sh --recheck`:
  each train's base still the plan's, or, for a train the run pushes, that version from that commit) before logging
  in, so re-running an old run cannot publish a stale version, then copies the trains the plan pushes into `push/`,
  attests `push/` alone, and pushes. A train nuget.org already has was attested by the run that pushed it, and a
  placeholder is never pushed.
- The core is pushed, and listed by nuget.org, before the satellites, which depend on it, are pushed: each train has a
  `NuGet/login` step and a push step of its own (`NUGET_LIST_TIMEOUT` 1800), the core's first, each login just before
  its push, since the key expires an hour after it is issued. So the plan job fails unless `release-trains.mjs trains`
  lists exactly `core satellites`: a train added or renamed gets its steps there first, or it would be packed and never
  pushed.
- A dispatch with `publish` unticked rehearses a run: the plan, the suites, the pack and `heavy.yml` run, and the
  `publish` and `docs` jobs are skipped (`github.event_name != 'workflow_dispatch' || inputs.publish`).
- The scripts name every package by id and version, never by a glob of one version: the trains share
  `artifacts/packages` and may share a number. `scripts/release-pack.sh` takes `<train>=<version>:<type>[:<last>]` per
  train of `src/release-trains.json`, each train held to its own baseline; `scripts/verify-packages.sh` the
  `<id>=<version>` pairs `release-trains.mjs versions` prints; `scripts/push-packages.sh` the ids of one version.
- `release-pack.sh`'s types: `major`, `minor` or `patch` a version nuget.org lacks for every package of the train, and
  above every stable version one has. A plan made at the tip of `main` never gives another; one made below a release
  published since does (a re-run of an older run, or a `release.yml` dispatch queued behind that release), and the
  message names the tag when it finds one on a commit `HEAD` does not descend from. The other causes: a missing tag, or
  a package moved in behind its last version (ADR 0012). `repair` is what `preview-gate.sh` gives a version nuget.org
  has, partly or whole, or a train it packs beside a repair and never pushes, looked up nowhere and held to no baseline.
  For a train that does not release while another does, its last release is packed again beside it, the version
  `<last>`, read from its tag (`<prefix><last>`, which the clone must hold): a package that release shipped has its
  project in the tag's tree, packable, on the train that commit's list gives it (`HEAD`'s for a commit from before the
  list), and every one must list `<last>` on nuget.org, those `HEAD` no longer packs on the train included, or that
  release stopped midway. `strict` is the core's: every package of it must list `<last>` too, a package that joined
  since included, which only a release of the core publishes unless it moved in from a train that released that number,
  and none may have a stable version above `<last>` (a core release cut after `HEAD`, which the message names, a missing
  tag, or a move ADR 0012 refuses); it is held to exactly that release's API (`StrictBaselineTrains`, which
  `src/Directory.Build.props` turns into `EnableStrictModeForBaselineValidation`), since the other trains are built
  against it. `keep` is any other train's, with no baseline; a package that joined the train since passes. Only
  `release.yml`'s per-train rehearsal passes `strict` or `keep` yet, for a train its plan does not release.
- `scripts/release-pack.test.mjs` replays those checks against the repository's projects and tags (strict and keep pack
  again `v0.2.1` and `satellites-v0.2.1`) and a flat container `node:http` serves (`NUGET_FLAT_CONTAINER`), and stops
  before `dotnet pack` (`RELEASE_PACK_CHECK_ONLY=true`), seams no workflow sets. A case needing other tags reads them
  from a throwaway `GIT_DIR` that borrows the repository's objects: a release whose list put a satellite on the core,
  one with a project since removed, one cut after `HEAD`, and the first release alone, for the cases about a version
  nuget.org has, so that no release fetched later changes their message. Replayed: strict whole, partial, with a package
  joined since, with one moved out since, with a stable version above `<last>`, behind a release cut after `HEAD`,
  without `<last>`, and at a version other than it; keep whole with packages joined since, with one its list put on
  another train, partial, with a package since removed, and without its tag; a version nuget.org lists and a stable
  version above it, for a train held to a baseline and for one that is not, with the release's tag cut after `HEAD` and
  merged into it, and a stable version between `<last>` and the version, which passes; a preview with a package new
  since its baseline; a train none of whose packages has `<last>`; a repair; an HTTP error and an empty index; a set
  other than `RELEASE_PACKAGE_IDS`, and none; no argument, and malformed, repeated, missing and unknown trains. Not
  replayed: a list naming no train, a failed lookup in the walk of a tag's tree, a tag's list that cannot be read, and
  everything from the pack on. It evaluates the projects, so `release tooling` installs the SDK before it runs the
  scripts' tests.
- `scripts/verify-packages.sh` also reads each nuspec's dependencies on `AdCodicem.ValueObjects*`: each must be a
  package of the set, at `[V, upper)`, V its version in the set and upper the next that may break it (`-0` closing a
  prerelease's). Every run packs every train, so no dependency of ours points outside the set. Each package's README
  must open with `# <id>` and keep no `{{placeholder}}` of `src/PackageReadme.md`.
- `scripts/push-packages.sh` pushes dependencies first and only what nuget.org lacks, in up to three passes, then waits
  until nuget.org lists every package. A push stopped midway is completed by those passes, by **Re-run failed jobs**, or
  by the next run (`repair`).
- Every run that decides, publishing or not, redeploys the site (`website/CLAUDE.md`), labelled with the core's
  version (`docs-version`); a run in which a job failed, or a rehearsal, deploys nothing.

## Stable releases (`release.yml`)

- `heavy.yml` runs on the dispatched commit and the packages the `pack` job uploaded, compat blocking
  (`compat-blocks: true`), and the `release` job needs it: it may run without it only on a dry run or when nothing was
  packed, the two cases that skip it. The `nuget-stable` reviewer is asked once it has passed.
- Dispatch `preview.yml` first and start the release once that run is green: the `was it previewed` job
  (`preview-gate.sh --report`) only warns, never blocks, when the release ships package inputs no nuget.org version
  carries.
- The `pack` job holds no credential: it computes the version with `next-version.mjs`, packs `src/` through
  `scripts/release-pack.sh` and freezes the documentation through `scripts/docs-snapshot.sh`. Once the `nuget-stable`
  reviewer approves, the `publish` job checks those files against the digests the pack job output, and semantic-release
  refuses to go on unless its version is the one packed, writes `CHANGELOG.md`, commits it with the snapshot, tags,
  pushes through `push-packages.sh`, and the site is redeployed. No build code runs beside the NuGet key, the App token
  or the OIDC token.
- `CHANGELOG.md` and `CHANGELOG.satellites.md` open with their title, which ends with the line
  `<!-- Releases are prepended below this line. -->`, every release below it. `.releaserc.json`'s `changelogTitle` is
  `CHANGELOG.md`'s title, verbatim: `@semantic-release/changelog` keeps a title on top only when the file starts with
  it, and otherwise writes the title, the notes, then the whole file. Change the title and the option together;
  `release-train-plugin.test.mjs` holds them equal.
- `scripts/release-train-plugin.mjs`, with `scripts/release-train.releaserc.json`, releases one train through
  semantic-release (ADR 0012), from the plan the pack job computes with `next-version.mjs --trains`: its `trains_json`,
  in a file `RELEASE_TRAIN_PLAN` names, the train in `RELEASE_TRAIN`, the release commits this run made for its other
  trains in `RELEASE_RUN_COMMITS` and the version packed in `PACKED_VERSION` (its header lists them, each required, an
  empty one included). The lists travel in the file, never in a variable: every process semantic-release starts
  inherits its environment, and Linux refuses to start one with a variable above 128 KiB, about 3,200 hashes. It fails
  when semantic-release counts from another tag than the plan's, sees a commit the plan did not walk, this run's other
  release commits aside, or does not see one the plan walked, a `[skip release]` one aside. The type is the plan's
  (`release-trains.mjs releaseType`, the line rule included), the version must be the one packed, a dry run included,
  and the train's changelog is written below its marker, refused with a release above it. The notes list the train's
  commits, `[skip release]` ones included (the row's `notes`), the core's with the commits no train owns, their breaking
  marks made plain since they ship in no package, and any other train's end with the core it is packed against. The
  configuration's analyzer rules, the slot aside, and notes options are `.releaserc.json`'s, which the test holds; its
  release commit is `chore(release): <tag> [skip ci]`, and its GitHub Release names the train's bundle
  (`AdCodicem.ValueObjects.satellites.<version>.sigstore.json` for the satellites). A caller's options take precedence
  over the file's (`tagFormat`, `dryRun`). `release.yml` runs it through `scripts/release-train.mjs`, as a dry run
  only, until it releases each train on its own.
- `scripts/release-train-plugin.test.mjs` replays it in throwaway repositories (two trains at `0.2.1`, this repository's
  scripts, configurations and changelogs, its `node_modules` linked, a bare remote), the plan computed there by
  `next-version.mjs --trains`: a dry run per train of each history of its table (the analyzer's options, a train's own
  breaking notes, `[skip release]` commits, and breaking commits no train owns, one of them reverted, among them), a
  merge, a range of 3,300 commits, each refusal, and a real run of the core then the satellites. semantic-release runs
  in a child process, since it hooks its process's standard streams, which carry `node:test`'s reports, with the
  configuration minus its GitHub entry and an environment holding the plan alone, no CI variable. It also renders both
  configurations' GitHub Release bodies.
- `scripts/release-train.mjs` releases one train through semantic-release's JavaScript API, from the plan, in the
  release job, the core first (its header holds its arguments, environment and outputs). A releasing train's
  `.nupkg` and `.snupkg` are staged in `artifacts/release/`, the folder the configuration's GitHub assets name, since
  `@semantic-release/github` does not template their paths. semantic-release runs with the configuration spread
  first, so that the driver's `tagFormat` and `dryRun` win, and `.releaserc.json`, which semantic-release still reads
  beside it, must hold no key the configuration lacks. A releasing train must come out at the plan's version, its tag
  on its release commit at `HEAD`, here and on origin, and a train the plan does not release must release nothing; a
  releasing dry run semantic-release declines ("behind the remote") fails, since main moved.
- The driver's sync, before semantic-release: every commit main holds since `RELEASE_REV`, the dispatched commit,
  must be another train's release commit of the plan (`chore(release): <tag> [skip ci]`, one parent, touching only
  the configuration's git assets), its tag on origin: an earlier train's, or, on a re-run, a later one's. The driver
  hands them to the plugin as `RELEASE_RUN_COMMITS`. It
  then restores `website/` and fast-forwards `HEAD`, since semantic-release declines a branch behind its remote, and
  only then unpacks `documentation.tar`: a fast-forward refuses untracked files it would overwrite, and the first
  train that releases commits the snapshot. Any other commit fails a releasing train ("main moved since the
  dispatch"), and leaves one that releases nothing unrun, with a notice.
- The driver's resume, before the sync, on a run that is not a dry run, for a releasing train: its tag on origin, or
  its release commit on main untagged (semantic-release pushes the commit in prepare, then the tag), which the driver
  tags and pushes, finishes the train whatever main holds since, without semantic-release, which would count from the
  last tag merged and release the version again. The release commit must be this dispatch's: it descends from
  `RELEASE_REV` past the other trains' release commits alone, and main holds it. Its GitHub Release is then
  completed: done when published, or when the draft carries every staged file at its digest; a missing file, or one
  not `uploaded`, is uploaded; a file at another digest, or a release commit another dispatch made, is another run's
  and fails; with no release, `@semantic-release/github` drafts it with the release commit's notes. So
  **Re-run failed jobs** completes a tagged train. git reads the token from its environment
  (`GIT_CONFIG_*`, an extra header as `actions/checkout` writes it), never its command line.
- Before either, a dry run included, the driver refuses a release commit of any train of the plan that the dispatched
  commit holds untagged (since that train's last tag, its tag absent from origin): a dispatch from main after a run
  stopped between its two pushes, which semantic-release would release a second time. Every train is checked, so that
  the core's step refuses before any train is released. Re-run failed jobs on that run completes it.
- `scripts/release-train.test.mjs` replays the driver in throwaway repositories whose origin,
  `https://github.com/owner/repo.git`, git rewrites to a bare remote (`url.<remote>.insteadOf`), against a fake GitHub
  API that `node:http` serves on `127.0.0.1`, with GitHub Actions' variables: a dry run of each train for three
  histories, a real run of the core then the satellites and its re-run, a tag push refused once then resumed, a draft
  missing a file, a file half uploaded, a file at another digest, a published release, a tag on another commit, a
  release a later dispatch made, a new dispatch at the core's or the satellites' release commit left untagged, a merge
  between the trains, a train that releases nothing past a merge, or re-run past a later train's release commit, the
  documentation left unpacked before a fast-forward and a page it drops, a commit that only looks like a release
  commit, tagged or not (it writes more, it is a merge, main does not hold it, the tag names another subject, two of
  them), a tag the clone names elsewhere, a release that leaves no release commit, the version checks past a
  configuration without the plugin, the token's header over HTTP, and each refusal of an argument, a plan or a
  configuration. A re-run is the same clone
  moved back to the dispatched commit with origin's tags only, as a fresh checkout is.
- `release.yml`'s `trains` input rehearses the release of each train, and needs `dry_run`: the pack job's first step
  fails otherwise, and the drivers always run with `--dry-run`. The pack job's `Release plan` step writes
  `artifacts/plan/plan.json`, `next-version.mjs --trains`'s `trains_json` with each train's ids from
  `package-ids.sh --trains`, uploaded as `release-plan` and checked in the release job against the digest the pack job
  outputs: never a job output or a variable, which its lists of hashes outgrow. A releasing train is packed at its
  next version; one that is not, at its last release again, `strict` for the core, `keep` for any other; the
  documentation is frozen at each train's version. The release job leaves `documentation.tar` packed, checks that
  `release-trains.mjs trains` lists `core satellites`, a step for each, and runs the driver for the core, then the
  satellites, each step's summary showing the version and the notes. The one-version steps skip on such a run.
- GitHub refuses a release body above 125,000 characters, and a train's notes grow with its range (about 160 characters
  a commit, the core's carrying every commit no train owns): both configurations' `releaseBodyTemplate` cut the notes at
  the last line below 100,000 characters and link the train's changelog at the tag, which holds them whole, as the
  release commit's message does.
- `scripts/docs-snapshot.sh` takes the one version, or each train's (`core=X satellites=Y`: every train of
  `src/release-trains.json` once, each stable). The line it freezes is the core's, and the entry it writes in
  `website/released-versions.json` is that version or `{"core": X, "satellites": Y}`. `scripts/docs-snapshot.test.mjs`
  runs it with `DOCS_SNAPSHOT_ENTRY_ONLY=true`, which stops once that entry is written: no workflow sets it, since a
  release commits the snapshot the rest of the script writes.
- The core's MinVer and semantic-release read the same `v*` tags (`tagFormat` in `.releaserc.json`), each other
  train's MinVer its own (below); `RELEASE_TRAIN_VERSIONS` hands MinVer the stable version.
- Without `RELEASE_TRAIN_VERSIONS` (`ci.yml`'s `build`, `heavy.yml`'s compat when no caller hands it packages, a local
  pack), MinVer reads each project's train's tags: `src/Directory.Build.props` sets `MinVerTagPrefix` to `v` for the
  core and `<train>-v` for any other, as `tagPrefixOf` in `scripts/release-trains.mjs` names them, and `ci.yml`'s
  pack check compares the two (`package-ids.sh --prefixes`); the root `Directory.Build.props` keeps `v` for everything
  outside `src/`. A pack that finds no tag of its train, and would ship `0.0.0-preview.0.<height>`, fails
  (`_RefuseUntaggedVersion`, `src/Directory.Build.targets`), so every checkout that packs fetches the tags
  (`fetch-depth: 0`).
- A tag-derived pack of a commit tagged stable for the satellites alone fails with NU5104: its satellites depend on the
  core's next prerelease, which a stable package cannot; a commit tagged for the core alone packs. Twin-tagged releases
  never meet it, and once the trains release on commits of their own (ADR 0012), `[skip ci]` keeps CI off such a
  commit; never dispatch `heavy.yml` at its ref.
- The GitHub Release links every package to nuget.org from `RELEASE_PACKAGE_IDS`, which the pack job evaluates with
  `scripts/package-ids.sh` and passes in semantic-release's starting environment: the body renders from it, so a prepare
  step cannot feed it. `release-pack.sh` fails before any push when the packed set differs from it.
- `attest provenance` signs the packages and every assembly inside them with a Sigstore SLSA provenance attestation.
  nuget.org re-signs each `.nupkg`, so its digest changes, but leaves the assemblies as packed, which therefore verify
  (`SECURITY.md`). `preview.yml` attests the same subjects before its push.
- Releases are immutable on this repository, so semantic-release creates a draft (`draftRelease`) and
  `attest provenance` publishes it once the bundle is attached; never publish the draft by hand. The draft's URL dies on
  publication, which is why `.releaserc.json` overrides `successComment` to link to the tag.
- `v0.1.0` is a baseline tag placed by hand, not a release (no `0.1.0` package exists): semantic-release ignores
  prerelease tags on a stable branch and would otherwise have started at `1.0.0`.
- Until each train computes a version of its own, `tag the other trains` (needing `release`, and only once it released)
  puts `<train>-v<version>` (`satellites-v<version>`) on the commit `v<version>` names, through the API with the job's
  token, and fails when the tag already names another commit; `docs/maintaining.md` places it by hand. A tag is read
  through `repos/…/commits/<tag>` or `git rev-parse <tag>^{commit}`, never `git/ref`, which answers an annotated
  tag's own object. `release tooling` (`ci.yml`, step `Every stable release tags each train`) fails while a `v<version>`
  from `0.2.1` on lacks its twin on the same commit.
- `preview.yml` and `release.yml` share the workflow-level concurrency group `release` (`cancel-in-progress: false`,
  `queue: max`): a preview never decides, packs or pushes while a release tags and pushes, and a release waiting for
  the `nuget-stable` reviewer, a dry run included, holds every preview until it ends.

## Package inputs and shipped dependencies

- `scripts/preview-gate.sh`'s `inputs` lists what changes a package: `src/` (the solution filter and the package
  READMEs included; `src/**/AnalyzerReleases.*.md` and `src/**/CLAUDE.md` excluded), `icon.png`, the root
  `Directory.Build.*` files, `global.json`, `nuget.config`, `.gitattributes`, `scripts/release-pack.sh`, and
  `Directory.Packages.props`: any change outside its `<PackageVersion>` elements, and the `<PackageVersion>` of any
  package in the restore graph of `src/`, build-time ones included (MinVer, SourceLink, PolySharp, Roslyn). That is wider
  than `shipped-dependencies`, so a `chore(deps)` bump of MinVer still publishes a preview. Add anything else that
  changes a package's bytes or metadata, or it never reaches a preview, and to `scripts/release-trains.mjs` as well
  (`GLOBAL_INPUTS`, or a rule of `trainsOfPath`), which says which trains it releases: a drift test of
  `release-trains.test.mjs` holds every tracked path to be an input of both or of neither, the two files read by
  content (`Directory.Packages.props`, the solution filter) aside, so a change to `preview-gate.sh` runs
  `release tooling`.
- `shipped-dependencies` must match the packed nuspecs: `ci.yml` (`scripts/shipped-dependencies-check.sh`) fails on a
  nuspec dependency the list lacks or a line matching nothing. `dependabot-auto-merge.yml` retitles a pull request
  updating a listed dependency `fix(deps):`, or `fix(deps)!:` across a major. Dependabot pull requests are squash-merged,
  so that the title becomes the commit semantic-release reads; a rebase keeps `chore(deps)` and releases nothing.
- A Dependabot security update pinning a transitive dependency the list does not name yet opens as `chore(deps)` (with
  no prefix when grouped) and fails `build`, so `build and test`. The workflow reads the list from the base branch and
  ignores your events, so retitle the pull request `fix(deps): …` first, then push the list line to its branch. Never
  land the line in a pull request of its own: nothing on `main` ships that ID yet, so the check fails there too.
- An update needing a later `Microsoft.Extensions.Options` or `Microsoft.Extensions.DependencyInjection.Abstractions`,
  transitive pins Dependabot never moves, fails its restore with NU1109 and opens no pull request: move those pins by hand
  with the rest of Microsoft's patch, in a `fix(deps)` commit (`docs/maintaining.md`, Routines).

## Workflows

- Pin every third-party action to a commit SHA with the release as a same-line comment
  (`uses: actions/checkout@3d3c42e… # v7.0.1`): Dependabot derives the semver bump from the comment, and a pin without one
  falls out of the `actions` group and may auto-merge as a non-major. Dependabot's `github-actions` entry lists
  `/.github/actions/*` beside `/`, since it does not look into a composite action's folder.
- `codecov/codecov-action`, `ossf/scorecard-action` and `github/codeql-action` publish annotated tags: re-pin with
  `git ls-remote <repo> 'refs/tags/vX.Y.Z^{}'`. Without `^{}` you get the tag object's SHA, which GitHub refuses.
- A local reusable workflow (`./.github/workflows/deploy-docs.yml`) or action (`./.github/actions/build-site`) stays
  unpinned: GitHub rejects `@ref` on either.
- actionlint is a downloaded binary, not an action: bump `ACTIONLINT_VERSION` and `ACTIONLINT_SHA256` together in
  `lint.yml`'s `workflows` job, the digest taken from the release's checksums file; Dependabot never updates it.
  `actionlint.yaml` silences `queue: max`, which actionlint does not know yet, in `deploy-docs.yml`, `preview.yml` and
  `release.yml`.
- `release tooling` runs every `scripts/*.test.mjs` through a quoted glob, `node --test '.github/scripts/*.test.mjs'`,
  never a directory argument, which fails from Node 21 on ("Cannot find module"): a new script's tests need no step.
- `held-back-majors.yml` (Mondays, before the preview) lists the majors Dependabot holds back:
  `scripts/held-back-majors.mjs` reads `dependabot.yml`'s ignore rules and the pins they match
  (`Directory.Packages.props`, `.config/dotnet-tools.json`, `package.json` at `package-lock.json`'s versions), against
  nuget.org's and npm's newest stable version, and keeps one open issue, labelled `dependencies`, with the list and the
  rules that match no pin. An edit notifies nobody, so it comments when the hidden marker on the report's first line,
  each row keyed by its pin and the line it waits for, changes. It never fails on a finding, only on a failed lookup
  or a `dependabot.yml` shape its narrow reader cannot place (an entry not opening with `package-ecosystem`, a key or
  comment on the `ignore:` line, an ignore rule with a key other than `dependency-name` and `update-types`, by name
  alone, or under an entry with no reader for its pins, or fewer rules read than `dependency-name` lines): give it a
  reader in the same change. A change to `dependabot.yml` runs `release tooling`,
  whose tests read the repository's own file.
- Trusted Publishing is keyed on a workflow file and an environment (`preview.yml` with `nuget`, `release.yml` with
  `nuget-stable`): change the nuget.org policy before renaming either file or moving its publish job.
- Required checks are job names: `build and test` (`ci.yml`), `workflows` and `conventional commits` (`lint.yml`). Never
  rename them without updating the `Default` ruleset. No job of `heavy.yml` is one, nor ever becomes one (ADR 0013).
- `build and test` is `ci.yml`'s `gate` job, an aggregator (ADR 0013): `if: always()` and `needs` on `plan`, `format`,
  `unit` (a matrix over the plan's `families`, `unit (<family>)` restoring, building and testing that family's project
  of the unit suite alone; `needs` reads the matrix as one job), `build` (through a solution filter in the runner's
  temporary folder, the projects `dotnet-affected` marks, or every project of the solution when everything runs, but
  the unit suite's; then what the plan selects of the generator and RDG suites, the compiled model, the pack and their
  checks), `release-tooling` and `heavy`, failing unless the plan succeeded and each job of its `expected` output
  succeeded; `expected` always lists `plan`, and `unit` whenever a family runs, and a job left out of it may be
  skipped, never failed or cancelled. Never give it an `if` that can skip it, or let it pass an expected job that
  failed, was cancelled or was skipped: a required check that is skipped reads as a success.
- `scripts/ci-plan.sh` plans each run from `ci-families.json` (its header holds the rules): a push to `main` runs
  everything; a pull request runs the families and lanes the files of its merge commit select, those of the projects
  (`src/`, the unit suite's projects and `tests/UnitTests.props`, `tests/Fixtures/`, the .NET code of the other lanes)
  through `dotnet-affected` (`.config/dotnet-tools.json`, installed alone into a folder of its own, since
  `dotnet tool restore` would fetch DocFX's 100 MB), which `--graph-needed` tells the workflow to install the SDK for.
  The projects it marks select the families: a package its own, one of core every family, a unit project its family.
  A file no rule names runs everything, so a new kind of file is safe before it is classified; give it a rule when it
  should not. Any change to `Directory.Packages.props` runs everything: `dotnet-affected` misses a transitive pin no
  project references, and Dependabot's NuGet pull requests wait for `heavy.yml` anyway. `preview.yml` and
  `release.yml` never call `ci.yml`: they run every suite whole, the integration suite through `heavy.yml` and the
  others in their `suites` job, the unit suite as each family's project from the table's `tests` in turn, its step
  failing once all have run when any failed.
- Every run first checks `ci-families.json` against the repository, without a build (`check_table`): every project
  under `src/` in exactly one family; every family's `tests` a project of the unit suite, and every
  `tests/AdCodicem.ValueObjects.UnitTests*/` project one family's; every tracked project but `tests/Compat/`'s in
  `AdCodicem.ValueObjects.slnx`, where `dotnet-affected` looks; every `Compile` item of `tests/UnitTests.props` or of a
  unit project's `.csproj`, whatever its `Link`, matching a file; no source taken from another project's folder holding
  a `[Fact]`, a `[Theory]` or a `ValueObjectContract<…>` class, since its tests would run in two projects, but
  `RuntimeClosingTests.cs` and `DescriptorTests.Registered.cs`, which every project runs over its own family;
  `src/release-trains.json` as `scripts/release-trains.mjs check` holds it, and each family's projects on one release
  train; and `codecov.yml`'s flags as `ci-plan.sh --codecov-flags` prints them.
- `src/release-trains.json` is the one list of release trains (ADR 0012), read twice: by MSBuild, at evaluation, with
  a regular expression that holds whatever the order of the keys (`src/Directory.Build.props`), and as JSON by
  `scripts/release-trains.mjs` (Node built-ins only: the plan job runs the runner's Node). `check` refuses every shape
  the expression could misread (a nested object, a name in two trains, a brace or an MSBuild expansion in a string),
  a stale name, a `PackageId`, and a core project referencing another train; `ci.yml`'s pack check compares the two
  readings (`package-ids.sh --trains`), and `release tooling` runs `release-trains.test.mjs`. The list is a property
  function's read, invisible to `dotnet-affected`, so the plan runs everything for it.
- `release-trains.mjs` also says which trains a commit releases (ADR 0012), the first rule that matches winning: a
  `CLAUDE.md` or `AnalyzerReleases.*.md` under `src/` none; a global input (`GLOBAL_INPUTS`, `nuget.config` in any case)
  every train; a `<PackageVersion>` the trains whose restore graph holds its package, or the train of the project its
  `'$(MSBuildProjectName)' == '<X>'` condition names (none for a project outside `src/`), any other change of
  `Directory.Packages.props` every train; the solution filter the trains of the projects it adds or removes (every train
  when it does not read as JSON); a `src/Shared/` file the trains linking it in the commit or its parent (every train
  when none does), which is why `check` refuses a `<Compile>` reaching outside its project's folder (`..`, a property or
  an item) but as one literal `<Compile Include="..\Shared\<file>" />` on one line, and an `<Import>` or a
  `Directory.Build.*` file in a project's folder, since it reads a project's items off its csproj alone; a project's
  folder its train; anything else under `src/` every train; the rest none. `commits <from> <to>` lists them;
  `lint-commits`, in `lint.yml`'s `conventional commits` job (`continue-on-error`), reads no graph, shows `deps` for a
  `<PackageVersion>`, and warns on a releasing commit whose project folders span two trains, `src/Shared/` and the
  global inputs aside, a project the tree no longer holds counting with the train it rode in the commit's parent.
  That text rule is a fast first guard and MSBuild the authority, at `HEAD`: `compile-items`, in `release tooling` after
  the restore (a project file under `src/` runs that job too, `ci-plan.sh`), evaluates each project's `Compile` items
  (`dotnet msbuild -getItem:Compile`, Release, each framework) and fails on any outside `src/<P>/`, `artifacts/` and
  the NuGet package folders but a `src/Shared/` file its csproj links literally, so that no import or condition compiles
  there a file another train releases, the check itself being `compileItemFaults`, which the tests run on recorded
  answers without dotnet. `check` also refuses an escape (`%2e%2e`) in a `Compile` item, which the text attribution of
  an older commit's tree could not read, and a `Compile` item, a `CreateItem` or a task's `Output` into `Compile` inside
  a `<Target>`, which evaluation does not see.
- The `coverage` matrix job, which the gate does not need, downloads the `coverage-*` artifacts
  (`coverage-unit-<family>`, `coverage-build`) and uploads one flag each: every unit report of the run under each
  family that ran, so that the lines of a family's packages another family's tests reach still count, and the
  generator and RDG reports under their own. One flag per upload: Codecov drops a carried-forward upload whole when
  an upload sharing any of its flags arrives, so an upload carrying several flags would lose the other families' lines
  on the next commit.
- `scripts/coverage-modules.sh <report> <family>` fails each `unit (<family>)` job whose report misses a package of its
  family, or covers no line of one, the generator and the code fix aside: `build` holds those two to the generator
  suite's report with `--generator`.
- A job that must block a merge goes into the `gate` job's `needs`, and into `scripts/ci-plan.sh`'s `expected` whenever
  it runs, never into the `Default` ruleset: a check per job would touch the ruleset at every rename (ADR 0013,
  Rejected).
- `heavy.yml` holds `native AOT`, `native AOT (Marten)`, `integration tests` (with `compiled-model.sh jit`, the SQL
  Server round trip) and `compat (.NET 11)`. It runs by itself on every push to `main` and on dispatch, and is called
  by `ci.yml` when `ci-plan.sh` says so (a pull request labelled `ci:heavy`, or one Dependabot opens on a
  `dependabot/nuget/` branch, every heavy job for now), and by `preview.yml` and `release.yml` before their publish
  job. `ci.yml` never calls it on a push: heavy.yml's own push run is the one main gets.
- `heavy.yml`'s jobs set their concurrency each, `heavy-<github.ref on a push, the run's id otherwise>-<job>`, so that
  a push to `main` cancels only an older push's heavy jobs: in a called workflow `github.workflow` is the caller's name,
  and a workflow-level group could match the caller's.
- `ci-plan.sh` reads the labels from the event, as the run starts: a `ci:heavy` label added later is seen by the next
  push, never by **Re-run jobs**. It only raises a notice when a pull request changes what only `heavy.yml` runs
  (`tests/NativeAot/`, `tests/Compat/`, the integration suite and its Functions fixture, `samples/`, `native-aot.sh`,
  `compiled-model.sh`, `heavy.yml`, Markdown aside).
- compat only informs unless the caller passes `compat-blocks: true`: `release.yml` does, `preview.yml` once .NET 11
  ships (`docs/maintaining.md`). Informing, the install of the island's SDK and its test steps take
  `continue-on-error`, each test step has a time limit below the job's, and a warning says it failed, so the job stays
  green: how a caller reads a job that fails under job-level `continue-on-error` is not documented. With no
  `packages-artifact-id`, it packs the commit under test itself; a caller's packages come by the artifact's id, never
  by name, and never with an empty id, which downloads every artifact of the run.
- Every job of `heavy.yml` runs, its conditions on its steps: a called job skipped by its own `if` can skip the
  caller's jobs that need the call even when the rest succeeded (a GitHub bug), and `preview.yml` would publish
  nothing. `preview.yml`'s `publish` and `release.yml`'s `release` jobs check `needs.heavy.result` explicitly.
- A run's artifacts share one namespace with the workflows it calls: `ci.yml`'s `coverage-unit-<family>` and
  `test-results-unit-<family>` (one per `unit` job), `coverage-build`, `test-results` and `packages`, `heavy.yml`'s
  `native-aot`, `native-aot-marten`, `integration-test-results` and `compat-test-results`, uploaded with
  `overwrite: true` for **Re-run all jobs**. Another job's name used twice fails the second upload, so a matrix job's
  names carry its family; the `coverage` job downloads `coverage-*` by pattern, a prefix no other artifact takes.
- No workflow but `ci.yml` names a job `build and test` (`preview.yml` and `release.yml` run theirs as `suites`): a
  ruleset matches a check by its name, and GitHub advises job names unique across workflows.
- After a red `unit (<family>)`, `build` or `heavy` job, **Re-run failed jobs** re-runs `build and test` with it; after
  a cancelled run, re-run all jobs or push again. A re-run of a green run keeps the previous `build and test` until the
  jobs it needs end.
- `scripts/ecmascript-patterns.mjs`, run by `ci.yml`'s `unit (text)` job, holds the Blazor package's pattern table
  (`Forms/EcmaScriptPatterns.json` of `tests/AdCodicem.ValueObjects.UnitTests.Text`) to Node's v-flag engine, as
  `EcmaScriptPatternTests` holds it to .NET: change both together. A change to the script runs the `text` family.
- `deploy-docs.yml` is `workflow_call` only, called by `preview.yml` and `release.yml`: redeploy the site by dispatching
  `preview.yml`, `publish` ticked. It builds a commit of its caller's run, or a stable release's tag descending from
  it: the core's `v<preview-version>`, or another train's `<train>-v<version>`, whose version need not be
  `preview-version`.
