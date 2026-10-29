# ADR-007: Public holidays — one open source behind a neutral port, requested by the App host only

**Status:** proposed · **Decided by:** owner (OpenHolidays as the source; the request names country, canton and
year; no credentials to any proxy) · agent (where, when, port, limits) · **Introduced by:** 002-holiday-planner

## Context

The holiday planner shows the person's cantonal public holidays without any entry (002 brief, outcome 3). The vision
allows one kind of request: the public holidays of the person's canton, naming only country, canton and year. The
page cannot make it: its Content-Security-Policy is `'self'` (ADR-006). Facts from requests to OpenHolidays for all
26 cantons and 2027, made during the design on 2026-10-29:

| Finding | Consequence |
|---------|-------------|
| Without `languageIsoCode` every entry carries its name in DE, EN, FR and IT | The request names exactly country, canton and year; a second language needs no new request |
| 7–20 KB and 8–23 entries per canton; every entry is one day | A cap far above that refuses nothing real |
| Entries reach the country (`nationwide`), the canton (`CH-ZH`) or only parts of it (`CH-AG-RF-MA`): AG and SZ 8 such days, SO 6, OW, FR and AI 1 each — Ostermontag among them in AG | Which days count is a business rule, not the adapter's call |
| `type` Public or Optional; `temporalScope` FullDay or HalfDay (3 half days, all part-canton) | Kind, reach and share are kept as the source gives them |
| A community project without SLA; its data repository is licensed ODbL 1.0 | The app names the source and its licence; data can be wrong or late |

## Decision

- **Source:** OpenHolidays, `GET https://openholidaysapi.org/PublicHolidays?countryIsoCode=CH&subdivisionCode=CH-<canton>&validFrom=<year>-01-01&validTo=<year>-12-31`,
  with `Accept: application/json` and `User-Agent: WorkTimeTracker` (no version, no system details) and no other
  header, cookie or credential.
- **Neutral port:** Application declares `IPublicHolidaySource`; it returns a domain `PublicHolidayYear` — per entry the
  date, its share (whole, half), its kind (public, optional, other), its reach (country, canton, part of the canton)
  and its names by language. The adapter translates and judges nothing; the domain decides which days count (002
  BR-014), as the history import records facts without judging them.
- **Where:** the adapter lives in `Wtt.PublicHolidays`, the only project with network code, referenced by the App and
  the developer CLI. The App host makes the request; the Recorder never does. The architecture test fails when the
  Recorder or Infrastructure references `Wtt.PublicHolidays` or uses `System.Net`, with negative probes.
- **When:** only while the App runs, once a canton is chosen, for last, this or next year, and only for a canton-year
  not yet stored — at App start for the current year, when the planner or the week shows such a year, after a canton is
  saved — and once per click on «Feiertage neu laden». After a failure: again at the next App start, or at the next
  need at least 1 h later.
- **Transport:** HTTPS with Windows' default certificate validation, revocation included; no redirect; no
  decompression; at most 256 KiB in at most 15 s. The system proxy is used without the person's credentials — a proxy
  that asks for a sign-in leaves the holidays unloaded, and the planner says so (owner).
- **Untrusted response:** read strictly for required fields, extra fields ignored; plausibility as 002 NFR-006; taken
  only whole and valid. Stored in the domain's terms with the source and the load time (ADR-008), never the raw body.
  A failure keeps the stored year and writes its reason, never the body, to the errors-only log.
- **Offline:** everything works with what is stored; a year not loaded says so, and counts follow once it arrives.
- **Attribution:** «Mein Jahr» names OpenHolidays and its licence.

## Rejected alternatives

| Option | Why not |
|--------|---------|
| The Recorder requests | Network code in the always-running autostart process; the App needs the answer at once after a canton change, and App and Recorder meet only in the data file |
| A separate fetcher executable | Another unsigned program for Smart App Control, ASR rules and Windows Error Reporting, for no isolation a project boundary does not give |
| The page requests | Ruled out by CSP `'self'`; the page would parse untrusted data |
| Nager.Date | Wrong on 2027 data where OpenHolidays was right |
| Holiday tables shipped with each release | The app has no update code: tables go stale after a year |
| Computing holidays in-house | 26 cantonal rule sets to maintain; the owner chose the source |
| Storing the raw response | An untrusted blob at rest, parsed again on every read |
| `languageIsoCode` in the request | A fourth parameter, and a new request for each language |
| The person's credentials to an asking proxy | Their Windows sign-in would leave the laptop for a public-holiday list (owner) |
| Revocation checks off | Weaker TLS to hide the certificate fetches; NFR-001 names them instead |

## Consequences

- NFR-001 gains one named exception (002 concept §1); ADR-002 and ADR-006 say the app's only network code is this.
- Wrong or missing data shows until the source fixes it and the person reloads; a format change needs an adapter fix
  and a release. The DNS lookup of `openholidaysapi.org` is visible to the network and carries nothing.
- ODbL asks for attribution of what is shown; the data stays on the laptop, so no database is conveyed.
- Behind a proxy that demands sign-in, public holidays never load.
- **Hard to undo:** low — one adapter; stored years carry their source.

## Sources

1. OpenHolidays API — https://www.openholidaysapi.org/en/ · https://openholidaysapi.org/swagger/index.html
2. Data licence (ODbL 1.0) — https://github.com/openpotato/openholidaysapi.data
3. SocketsHttpHandler (redirects, decompression, proxy credentials) — https://learn.microsoft.com/en-us/dotnet/api/system.net.http.socketshttphandler
4. HttpClient.DefaultProxy — https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpclient.defaultproxy
