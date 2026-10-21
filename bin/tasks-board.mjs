#!/usr/bin/env node
// Consumer-side resolver wrapper for the tasks-board plugin.
//
// THIS FILE IS GENERATED. It is emitted (with two emit-time name substitutions)
// into every initialized consumer repo by `tasks-board init`
// (scripts/init-consumer.mjs). Do not hand-edit the copy in a consumer repo;
// re-run init to refresh it. The canonical source lives at
// `templates/consumer-tasks-board.mjs` in the tasks-board plugin.
//
// Why this file exists: Claude Code, Copilot CLI, and Codex install plugins in
// different layouts and do not put `tasks-board` on PATH. This wrapper resolves
// the plugin location so package scripts, terminal users, and the generated
// consumer-local MCP registrations can share one stable consumer command.
// Once located, it spawns the plugin's own `bin/tasks-board.mjs` dispatcher with
// the original argv and forwards stdio/signals/exit code.
//
// Modes:
//   (default)      forward argv to the plugin's bin dispatcher.
//   --mcp          the committed `.mcp.json` registration
//                  (`node bin/tasks-board.mjs --mcp`): start the plugin's MCP
//                  launcher (`launch.cjs`) for the repository this wrapper
//                  lives in. The repository root comes from the wrapper's own
//                  location, so the committed registration carries no machine
//                  path.
//   --codex-mcp    the generated Codex registration: verify the git-ignored
//                  machine binding, then start `launch.cjs`.
//
// Normal command and --mcp resolution order (first usable install wins;
// "usable" = has the plugin manifest AND the bin dispatcher, plus `launch.cjs`
// in --mcp mode):
//   1. TASKS_BOARD_PLUGIN_ROOT env var
//   2. ~/.copilot/installed-plugins/ai-developer-toolkit/tasks-board/, only
//      when COPILOT_AGENT_SESSION_ID is set (Copilot CLI launched us, so its
//      own copy wins over other hosts' caches)
//   3. CLAUDE_PLUGIN_ROOT/tasks-board (Claude Code harness-set)
//   4. CLAUDE_PLUGIN_ROOT/cache/ai-developer-toolkit/tasks-board/<newest>
//   5. ~/.claude/plugins/cache/ai-developer-toolkit/tasks-board/<newest>
//   6. ~/.codex/plugins/cache/ai-developer-toolkit/tasks-board/<newest>
//   7. ~/.copilot/installed-plugins/ai-developer-toolkit/tasks-board/
//      (Copilot CLI install layout; single live copy, no per-version subdir)
//   8. consumerWrapper.localDevPluginRootRel from .tasks-board/config.json
//      (optional in-tree / submodule local-dev fallback, repoRoot-relative)
//
// `--codex-mcp` keeps an explicit override first, then prioritizes Codex's own
// versioned cache, and ignores COPILOT_AGENT_SESSION_ID. Every candidate in
// that mode must pass its package-owned, version-matched runtime inventory and
// per-file digest verification.
//
// The tasks-board dispatcher has no plugin-root runtime dependencies (it uses
// only Node built-ins + the pure scripts/lib core; the MCP and viewer runtimes
// live in their own on-demand caches), so "usable" does not require a
// node_modules sentinel.

import { execFileSync, spawn } from 'node:child_process'
import crypto from 'node:crypto'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import process from 'node:process'
import { fileURLToPath, pathToFileURL } from 'node:url'

// Emit-time substitution: init-consumer replaces these tokens with the real
// marketplace + plugin names so there is a single source of truth. The raw
// template carries placeholders (kept syntactically valid for editors/CI).
const MARKETPLACE_NAME = 'ai-developer-toolkit'
const PLUGIN_NAME = 'tasks-board'

const SELF = 'bin/tasks-board.mjs'
const MANIFEST_REL = path.join('.claude-plugin', 'plugin.json')
const DISPATCHER_REL = path.join('bin', 'tasks-board.mjs')
const LAUNCHER_REL = 'launch.cjs'
export const CODEX_RUNTIME_INVENTORY_REL = 'runtime-inventory.json'

function hasManifest(dir) {
    return Boolean(dir) && fs.existsSync(path.join(dir, MANIFEST_REL))
}

function hasDispatcher(dir) {
    return Boolean(dir) && fs.existsSync(path.join(dir, DISPATCHER_REL))
}

function isUsable(dir) {
    return hasManifest(dir) && hasDispatcher(dir)
}

function hasMcpLauncher(dir) {
    return isUsable(dir) && fs.existsSync(path.join(dir, LAUNCHER_REL))
}

function usableForMode(mode) {
    if (mode === 'codex-mcp') return hasCodexLaunchCapabilities
    if (mode === 'mcp') return hasMcpLauncher
    return isUsable
}

function missingCapabilityError(mode) {
    if (mode === 'codex-mcp') return 'missing-codex-launch'
    if (mode === 'mcp') return 'missing-launcher'
    return 'missing-dispatcher'
}

function sha256File(file) {
    const canonicalText = fs.readFileSync(file, 'utf8').replace(/\r\n/g, '\n')
    return crypto.createHash('sha256').update(canonicalText, 'utf8').digest('hex')
}

export function validateCodexRuntimeInventory(dir) {
    try {
        const inventory = JSON.parse(fs.readFileSync(path.join(dir, CODEX_RUNTIME_INVENTORY_REL), 'utf8'))
        if (inventory?.schemaVersion !== 2 ||
            inventory?.kind !== 'tasks-board-runtime-inventory' ||
            inventory?.marketplace !== MARKETPLACE_NAME ||
            inventory?.plugin !== PLUGIN_NAME ||
            typeof inventory?.version !== 'string' ||
            inventory?.digestMode !== 'sha256-utf8-lf' ||
            !Array.isArray(inventory?.files) ||
            inventory.files.length === 0) {
            return { ok: false, error: 'invalid runtime inventory metadata' }
        }
        const pluginManifest = JSON.parse(fs.readFileSync(path.join(dir, MANIFEST_REL), 'utf8'))
        const packageManifest = JSON.parse(fs.readFileSync(path.join(dir, 'package.json'), 'utf8'))
        if (pluginManifest?.name !== PLUGIN_NAME ||
            inventory.version !== pluginManifest?.version ||
            inventory.version !== packageManifest?.version) {
            return { ok: false, error: 'runtime inventory version mismatch' }
        }
        const paths = inventory.files.map(entry => entry?.path)
        const sorted = [...paths].sort()
        if (paths.some(relative => typeof relative !== 'string' ||
            relative.includes('\\') ||
            path.posix.isAbsolute(relative) ||
            relative.split('/').some(segment => segment === '' || segment === '.' || segment === '..')) ||
            new Set(paths).size !== paths.length ||
            paths.some((relative, index) => relative !== sorted[index])) {
            return { ok: false, error: 'invalid runtime inventory paths' }
        }
        for (const entry of inventory.files) {
            if (!/^[a-f0-9]{64}$/.test(entry?.sha256 ?? '')) return { ok: false, error: `invalid runtime inventory digest: ${entry?.path}` }
            const file = path.join(dir, ...entry.path.split('/'))
            if (!fs.statSync(file, { throwIfNoEntry: false })?.isFile()) return { ok: false, error: `missing runtime inventory file: ${entry.path}` }
            if (sha256File(file) !== entry.sha256) return { ok: false, error: `runtime inventory digest mismatch: ${entry.path}` }
        }
        return { ok: true, inventory }
    } catch (error) {
        return { ok: false, error: `runtime inventory unreadable: ${error.message}` }
    }
}

function hasCodexLaunchCapabilities(dir) {
    return Boolean(dir) && validateCodexRuntimeInventory(dir).ok
}

// Parse a directory name as a strict `<major>.<minor>.<patch>` triple. Names
// with any suffix (e.g. prerelease `1.2.0-rc.1`) are treated as non-semver and
// fall to the lexical tie-break below, so a clean stable version always
// outranks a same-core prerelease dir.
function parseSemver(name) {
    const match = /^(\d+)\.(\d+)\.(\d+)$/.exec(name)
    return match ? [Number(match[1]), Number(match[2]), Number(match[3])] : null
}

// Descending comparator: newest version first. Numeric semver beats lexical;
// non-semver names sort after semver names; equal/both-non-semver use lexical.
function compareDirsDesc(a, b) {
    const pa = parseSemver(a)
    const pb = parseSemver(b)
    if (pa && pb) {
        for (let index = 0; index < 3; index += 1) {
            if (pa[index] !== pb[index]) {
                return pb[index] - pa[index]
            }
        }
        return 0
    }
    if (pa) return -1
    if (pb) return 1
    return a < b ? 1 : a > b ? -1 : 0
}

// Return the newest usable versioned subdir under `base`, or null. A broken
// newest (manifest but no dispatcher) is skipped in favor of an older usable
// install, so `1.2.0`-without-dispatcher does not shadow a working `1.1.0`.
function newestVersionedSubdir(base, usable = isUsable) {
    if (!base || !fs.existsSync(base)) {
        return null
    }
    let names
    try {
        names = fs.readdirSync(base, { withFileTypes: true })
            .filter((entry) => entry.isDirectory())
            .map((entry) => entry.name)
            .sort(compareDirsDesc)
    } catch {
        return null
    }
    for (const name of names) {
        const dir = path.join(base, name)
        if (usable(dir)) {
            return dir
        }
    }
    return null
}

function usableResult(pluginRoot, source, warnings) {
    return { pluginRoot, source, warnings }
}

// Pure resolver. Inputs are injected so the function is unit-testable without
// touching the real process env/home. Returns one of:
//   { pluginRoot, source, warnings }                 -- usable install selected
//   { error: 'missing-dispatcher'|'missing-launcher'|'missing-codex-launch', ... }
//        -- explicit TASKS_BOARD_PLUGIN_ROOT lacks capabilities required by the mode
//   { error: 'not-found', warnings }                 -- total cascade miss
// `mode` is 'command' (default), 'mcp' or 'codex-mcp'.
export function resolvePluginRoot({ env = {}, homedir, cwd, config = {}, mode = 'command' } = {}) {
    void cwd
    const warnings = []
    const usable = usableForMode(mode)
    const copilotInstall = homedir ? path.join(homedir, '.copilot', 'installed-plugins', MARKETPLACE_NAME, PLUGIN_NAME) : null

    // 1. Explicit override.
    const explicit = env.TASKS_BOARD_PLUGIN_ROOT
    if (explicit) {
        if (hasManifest(explicit)) {
            if (usable(explicit)) {
                return usableResult(explicit, 'TASKS_BOARD_PLUGIN_ROOT', warnings)
            }
            // The operator explicitly pointed at a real plugin dir, but it is
            // broken (no dispatcher). Fail loudly instead of silently falling
            // through to a different install they did not choose.
            return {
                error: missingCapabilityError(mode),
                pluginRoot: explicit,
                source: 'TASKS_BOARD_PLUGIN_ROOT',
                warnings,
            }
        }
        warnings.push(`${SELF}: TASKS_BOARD_PLUGIN_ROOT=${explicit} is not a usable plugin install (missing ${MANIFEST_REL}); falling through`)
    }

    // 2. The launching host's own copy: Copilot CLI sets
    // COPILOT_AGENT_SESSION_ID for the processes it starts, so its installed
    // plugin wins over a Claude Code or Codex cache on the same machine.
    // Codex static launches keep their own order below.
    if (mode !== 'codex-mcp' && env.COPILOT_AGENT_SESSION_ID && copilotInstall && usable(copilotInstall)) {
        return usableResult(copilotInstall, '~/.copilot', warnings)
    }

    // Codex static MCP launches prefer Codex's own versioned cache and require
    // the complete static launcher/bootstrap inventory.
    if (mode === 'codex-mcp' && homedir) {
        const codexVersioned = newestVersionedSubdir(
            path.join(homedir, '.codex', 'plugins', 'cache', MARKETPLACE_NAME, PLUGIN_NAME),
            usable,
        )
        if (codexVersioned) return usableResult(codexVersioned, '~/.codex/cache', warnings)
    }

    // Claude Code harness-set plugin root.
    const claudePluginRoot = env.CLAUDE_PLUGIN_ROOT
    if (claudePluginRoot) {
        const direct = path.join(claudePluginRoot, PLUGIN_NAME)
        if (usable(direct)) {
            return usableResult(direct, 'CLAUDE_PLUGIN_ROOT', warnings)
        }
        const versioned = newestVersionedSubdir(path.join(claudePluginRoot, 'cache', MARKETPLACE_NAME, PLUGIN_NAME), usable)
        if (versioned) {
            return usableResult(versioned, 'CLAUDE_PLUGIN_ROOT/cache', warnings)
        }
    }

    if (homedir) {
        // 5. Claude Code user-scope plugin cache (versioned).
        const userVersioned = newestVersionedSubdir(path.join(homedir, '.claude', 'plugins', 'cache', MARKETPLACE_NAME, PLUGIN_NAME), usable)
        if (userVersioned) {
            return usableResult(userVersioned, '~/.claude/cache', warnings)
        }
        // 6. Codex versioned plugin cache.
        if (mode !== 'codex-mcp') {
            const codexVersioned = newestVersionedSubdir(path.join(homedir, '.codex', 'plugins', 'cache', MARKETPLACE_NAME, PLUGIN_NAME), usable)
            if (codexVersioned) {
                return usableResult(codexVersioned, '~/.codex/cache', warnings)
            }
        }
        // 7. Copilot CLI install layout (single live copy, no version subdir).
        if (usable(copilotInstall)) {
            return usableResult(copilotInstall, '~/.copilot', warnings)
        }
    }

    // 8. Consumer-configured local-dev fallback (e.g. an in-tree submodule).
    if (config.localDevPluginRoot && usable(config.localDevPluginRoot)) {
        return usableResult(config.localDevPluginRoot, 'localDev', warnings)
    }

    return { error: 'not-found', warnings }
}

// Read the consumer's local-dev wrapper hint from .tasks-board/config.json. A
// minimal JSON read — the wrapper cannot call the plugin's loadConfig() before
// it has located the plugin. Tolerates a missing/invalid config (the plugin's
// own loadConfig surfaces config errors with full diagnostics once it runs).
export function loadConsumerWrapperConfig(repoRoot) {
    const configPath = path.join(repoRoot, '.tasks-board', 'config.json')
    let localDevPluginRootRel = null
    try {
        if (fs.existsSync(configPath)) {
            const parsed = JSON.parse(fs.readFileSync(configPath, 'utf8'))
            const rel = parsed?.consumerWrapper?.localDevPluginRootRel
            if (typeof rel === 'string' && rel.length > 0) {
                localDevPluginRootRel = rel
            }
        }
    } catch {
        // Ignore — resolve via the install cascade instead.
    }
    const localDevPluginRoot = localDevPluginRootRel ? path.resolve(repoRoot, localDevPluginRootRel) : null
    return { configPath, localDevPluginRootRel, localDevPluginRoot }
}

function gitRepoRoot(cwd) {
    try {
        return execFileSync('git', ['-C', cwd, 'rev-parse', '--show-toplevel'], {
            encoding: 'utf8',
            stdio: ['ignore', 'pipe', 'ignore'],
        }).trim()
    } catch {
        return cwd
    }
}

export function normalizeRepoArguments(argv, cwd = process.cwd()) {
    const normalized = [...argv]
    for (let index = 0; index < normalized.length; index += 1) {
        const arg = normalized[index]
        if (arg === '--repo') {
            const value = normalized[index + 1]
            if (!value || value.startsWith('--')) throw new Error('--repo requires a value')
            normalized[index + 1] = path.resolve(cwd, value)
            index += 1
        } else if (arg.startsWith('--repo=')) {
            const value = arg.slice('--repo='.length)
            if (!value) throw new Error('--repo requires a value')
            normalized[index] = `--repo=${path.resolve(cwd, value)}`
        }
    }
    return normalized
}

function writeCascadeError() {
    process.stderr.write(`${SELF}: could not locate the ${PLUGIN_NAME} plugin.\n`)
    process.stderr.write('  Tried:\n')
    process.stderr.write('    1. $TASKS_BOARD_PLUGIN_ROOT\n')
    process.stderr.write(`    2. ~/.copilot/installed-plugins/${MARKETPLACE_NAME}/${PLUGIN_NAME}/ (when $COPILOT_AGENT_SESSION_ID is set)\n`)
    process.stderr.write(`    3. $CLAUDE_PLUGIN_ROOT/${PLUGIN_NAME}\n`)
    process.stderr.write(`    4. $CLAUDE_PLUGIN_ROOT/cache/${MARKETPLACE_NAME}/${PLUGIN_NAME}/<newest>\n`)
    process.stderr.write(`    5. ~/.claude/plugins/cache/${MARKETPLACE_NAME}/${PLUGIN_NAME}/<newest>\n`)
    process.stderr.write(`    6. ~/.codex/plugins/cache/${MARKETPLACE_NAME}/${PLUGIN_NAME}/<newest>\n`)
    process.stderr.write(`    7. ~/.copilot/installed-plugins/${MARKETPLACE_NAME}/${PLUGIN_NAME}/\n`)
    process.stderr.write('    8. consumerWrapper.localDevPluginRootRel (.tasks-board/config.json)\n')
    process.stderr.write(`  Fix: install the plugin via \`/plugin install ${PLUGIN_NAME}@${MARKETPLACE_NAME}\`\n`)
    process.stderr.write('       (or `copilot plugin install`), or set TASKS_BOARD_PLUGIN_ROOT to an absolute path.\n')
}

// The repository this wrapper was generated into: <repoRoot>/bin/tasks-board.mjs.
function wrapperRepoRoot() {
    return path.dirname(path.dirname(fileURLToPath(import.meta.url)))
}

function startMcpLauncher(pluginRoot, repoRoot) {
    const child = spawn(process.execPath, [path.join(pluginRoot, LAUNCHER_REL)], {
        stdio: 'inherit',
        cwd: repoRoot,
        env: {
            ...process.env,
            TASKS_BOARD_PLUGIN_ROOT: pluginRoot,
            TASKS_BOARD_REPO_ROOT: repoRoot,
        },
    })
    child.on('exit', (code, signal) => process.exit(code ?? (signal ? 1 : 0)))
    process.on('SIGINT', () => child.kill('SIGINT'))
    process.on('SIGTERM', () => child.kill('SIGTERM'))
}

async function main() {
    const argv = process.argv.slice(2)
    const codexMcp = argv[0] === '--codex-mcp'
    const mcp = argv[0] === '--mcp'
    const bindingIndex = argv.findIndex((arg) => arg === '--binding')
    const bindingArg = bindingIndex >= 0 ? argv[bindingIndex + 1] : null
    const commandArgv = codexMcp || mcp ? [] : normalizeRepoArguments(argv)
    const hasRepo = commandArgv.some((arg) => arg === '--repo' || arg.startsWith('--repo='))
    let repoRoot
    if (hasRepo) {
        const flagIndex = commandArgv.findIndex((arg) => arg === '--repo' || arg.startsWith('--repo='))
        const flag = commandArgv[flagIndex]
        repoRoot = flag.startsWith('--repo=')
            ? path.resolve(flag.slice('--repo='.length))
            : path.resolve(commandArgv[flagIndex + 1] ?? process.cwd())
    } else {
        repoRoot = codexMcp || mcp ? wrapperRepoRoot() : gitRepoRoot(process.cwd())
    }

    const { localDevPluginRoot } = loadConsumerWrapperConfig(repoRoot)

    const result = resolvePluginRoot({
        env: process.env,
        homedir: os.homedir(),
        cwd: process.cwd(),
        config: { localDevPluginRoot },
        mode: codexMcp ? 'codex-mcp' : mcp ? 'mcp' : 'command',
    })

    for (const warning of result.warnings ?? []) {
        process.stderr.write(`${warning}\n`)
    }

    if (result.error === 'not-found') {
        writeCascadeError()
        process.exit(2)
    }
    if (result.error === 'missing-dispatcher') {
        process.stderr.write(`${SELF}: plugin found at ${result.pluginRoot} but missing ${DISPATCHER_REL}\n`)
        process.exit(3)
    }
    if (result.error === 'missing-launcher') {
        process.stderr.write(`${SELF}: plugin found at ${result.pluginRoot} but missing ${DISPATCHER_REL} or ${LAUNCHER_REL}\n`)
        process.exit(3)
    }
    if (result.error === 'missing-codex-launch') {
        process.stderr.write(`${SELF}: plugin found at ${result.pluginRoot} but missing Codex launcher or binding dependencies\n`)
        process.exit(3)
    }

    if (mcp) {
        startMcpLauncher(result.pluginRoot, repoRoot)
        return
    }

    if (codexMcp) {
        const bindingModule = await import(pathToFileURL(path.join(result.pluginRoot, 'scripts', 'lib', 'codex-workspace-binding.mjs')).href)
        // The committed Codex block names the binding repository-relative;
        // resolve it against this wrapper's repository, never the process cwd.
        const bindingPath = bindingArg && !path.isAbsolute(bindingArg) ? path.resolve(repoRoot, bindingArg) : bindingArg
        const verified = bindingModule.verifyCodexWorkspaceBinding({
            bindingPath,
            wrapperPath: fileURLToPath(import.meta.url),
            pluginRoot: result.pluginRoot,
        })
        startMcpLauncher(result.pluginRoot, verified.repoRoot)
        return
    }

    const dispatcher = path.join(result.pluginRoot, 'bin', 'tasks-board.mjs')
    // The recovered consumer is default context, not an invented explicit
    // selector. Preserve caller aliases and JSON selectors for the dispatcher.
    const finalArgs = commandArgv
    const childEnv = {
        ...process.env,
        TASKS_BOARD_PLUGIN_ROOT: result.pluginRoot,
        TASKS_BOARD_REPO_ROOT: repoRoot,
    }

    const child = spawn(process.execPath, [dispatcher, ...finalArgs], {
        stdio: 'inherit',
        env: childEnv,
    })
    child.on('exit', (code, signal) => process.exit(code ?? (signal ? 1 : 0)))
    // Forward interrupt/terminate to the child. On Windows these signals are
    // best-effort: Node maps child.kill('SIGINT'/'SIGTERM') onto a terminate,
    // which is sufficient to tear down the spawned dispatcher.
    process.on('SIGINT', () => child.kill('SIGINT'))
    process.on('SIGTERM', () => child.kill('SIGTERM'))
}

// Correct ESM main-module guard: compare the resolved file path to argv[1].
// `import.meta.url === process.argv[1]` (a file:// URL vs a path) is ALWAYS
// false, so a wrapper using that form would never dispatch.
if (process.argv[1] && fileURLToPath(import.meta.url) === path.resolve(process.argv[1])) {
    main().catch((error) => {
        process.stderr.write(`${SELF}: ${error.message}\n`)
        process.exit(4)
    })
}
