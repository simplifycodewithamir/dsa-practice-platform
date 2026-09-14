# Sandbox hardening

What stops a submission from doing something other than solving the question, why each control is
there, and what is still not covered. Everything here is enforced by the Judge
(`DockerSandboxExecutor`) or by docker-compose, and asserted by `SandboxEscapeTests`.

## The threat

Anyone can submit code and we run it. The realistic goals of an attacker are, roughly in order of
how often they are actually attempted:

1. **Mine cryptocurrency** — the most common abuse of any free "run my code" service. Needs CPU and,
   to be worth anything, network access to a pool.
2. **Exfiltrate data or attack others** — read something from the host, or use it to reach other
   machines.
3. **Escape to the host** — via the kernel, via the Docker socket, or via a misconfigured mount.
4. **Deny service** — fork bombs, memory exhaustion, filling the disk, leaving processes behind.

## Controls

| Control | Set to | Stops |
|---|---|---|
| `NetworkMode: none` | no network at all | mining pools, exfiltration, attacking other hosts |
| `ReadonlyRootfs` | true | persisting anything; tampering with the interpreter |
| Writable space | 32 MB tmpfs at `/work`, in memory | filling the host disk; anything surviving the run |
| `CapDrop: ALL` | no capabilities | mounting, raw sockets, ptrace, most escalation paths |
| `no-new-privileges` | on | setuid binaries regaining privilege |
| User | `65534:65534` (nobody) | writing anywhere root could |
| seccomp | the daemon's default profile | the syscalls a container has no business making |
| `PidsLimit` | 64 | fork bombs |
| `Memory` + `MemorySwap` equal | the question's limit | memory exhaustion; **equal values disable swap**, without which the limit is soft |
| `NanoCPUs` | 1 core | starving other submissions on a small VM |
| Wall clock | question limit + 3s grace | infinite loops |
| Output capture | 64 KB | output floods |
| Container lifetime | one per test case, force-removed in a `finally` | anything left running after the verdict |
| Artifact volume (compiled languages) | mounted **read-only** by run containers | a program rewriting what runs for the next test case |
| Docker API access | via `docker-socket-proxy`, only containers/images/volumes, `EXEC=0` | a compromised Judge starting a privileged container |

### Why the socket proxy matters

The Judge needs the Docker API to create sandboxes. Mounting `/var/run/docker.sock` into it would
make the Judge root-equivalent on the host: anything that compromised the Judge could start a
container with `--privileged` and the host root filesystem bind-mounted. The proxy exposes only the
endpoints the Judge actually calls, and refuses everything else — including `exec`.

The proxy is not a substitute for the Judge being trustworthy; it narrows what a compromise buys.

## The one place something runs as root

The **compile** container for compiled languages runs as root, because a Docker volume is created
root-owned and nothing else could write the artifacts into it. It still has no network, no
capabilities and a read-only root filesystem, and it runs the compiler, not the submission.

Submitted code only ever *runs* as `nobody`, in a container with no volume mounted writable.

The alternative — a throwaway container that chowns the volume first — costs an extra container per
submission. Worth revisiting if the compile step ever runs anything less trustworthy than Roslyn.

## What is not covered

- **A Linux kernel exploit.** Containers share the host kernel; seccomp and dropped capabilities
  shrink the attack surface but do not remove it. The answer is **gVisor** (`runsc`), which puts a
  user-space kernel in between. The Judge supports it today via `Judge:Sandbox:Runtime: "runsc"`,
  which is passed straight through to the daemon — but **this is not verified**: gVisor can't be
  installed under Docker Desktop on WSL2, where this was developed. Install it on the production VM
  (item 25) and set that one option, then re-run `SandboxEscapeTests` against it.
- **Side channels.** Spectre-class attacks between containers are out of scope for a practice site.
- **Abuse by volume** — thousands of legitimate-looking submissions. That is rate limiting, item 22.
- **A malicious question.** Content is authored in the repository and reviewed in a PR, so this is a
  code-review problem rather than a runtime one.

## Verifying

`SandboxEscapeTests` submits code that tries to escape and asserts the kernel refuses: no Docker
socket, no capabilities, `NoNewPrivs`, no other processes visible, no mounting, no writing to
`/proc/sysrq-trigger`, no reading `/dev/sda`, no exceeding the pids cap after raising its own
rlimits, and nothing left running afterwards.

Run them against any host whose isolation you want to check — they are ordinary integration tests:

```bash
dotnet test --project tests/DsaPractice.Judge.IntegrationTests/DsaPractice.Judge.IntegrationTests.csproj \
  --filter-class "*SandboxEscapeTests*"
```
