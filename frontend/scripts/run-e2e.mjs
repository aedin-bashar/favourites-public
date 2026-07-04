import { spawn } from 'node:child_process';
import http from 'node:http';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const frontendDir = dirname(dirname(fileURLToPath(import.meta.url)));
const serverUrl = 'http://localhost:4200';
const isWindows = process.platform === 'win32';
const angularCli = join(frontendDir, 'node_modules', '@angular', 'cli', 'bin', 'ng.js');
const playwrightCli = join(frontendDir, 'node_modules', '@playwright', 'test', 'cli.js');

let serverProcess;
let startedServer = false;

process.on('SIGINT', async () => {
  await stopServer();
  process.exit(130);
});

process.on('SIGTERM', async () => {
  await stopServer();
  process.exit(143);
});

try {
  if (!(await isServerReady())) {
    startedServer = true;
    serverProcess = spawn(process.execPath, [angularCli, 'serve'], {
      cwd: frontendDir,
      detached: !isWindows,
      stdio: 'inherit',
    });

    await waitForServer();
  }

  const exitCode = await runPlaywright(process.argv.slice(2));
  await stopServer();
  process.exit(exitCode);
} catch (error) {
  console.error(error instanceof Error ? error.message : error);
  await stopServer();
  process.exit(1);
}

function isServerReady() {
  return new Promise((resolve) => {
    const request = http.get(serverUrl, (response) => {
      response.resume();
      resolve(response.statusCode !== undefined && response.statusCode < 500);
    });

    request.on('error', () => resolve(false));
    request.setTimeout(1000, () => {
      request.destroy();
      resolve(false);
    });
  });
}

async function waitForServer() {
  const timeoutAt = Date.now() + 120_000;

  while (Date.now() < timeoutAt) {
    if (serverProcess?.exitCode !== null) {
      throw new Error(`Angular dev server exited early with code ${serverProcess?.exitCode}.`);
    }

    if (await isServerReady()) {
      return;
    }

    await delay(500);
  }

  throw new Error(`Timed out waiting for Angular dev server at ${serverUrl}.`);
}

function runPlaywright(args) {
  return new Promise((resolve, reject) => {
    const child = spawn(process.execPath, [playwrightCli, 'test', ...args], {
      cwd: frontendDir,
      env: {
        ...process.env,
        FAVOURITES_SKIP_PLAYWRIGHT_WEBSERVER: '1',
      },
      stdio: 'inherit',
    });

    child.on('error', reject);
    child.on('exit', (code, signal) => {
      if (signal) {
        resolve(1);
        return;
      }

      resolve(code ?? 1);
    });
  });
}

async function stopServer() {
  if (!startedServer || !serverProcess?.pid) {
    return;
  }

  if (serverProcess.exitCode !== null) {
    return;
  }

  if (isWindows) {
    await new Promise((resolve) => {
      const killer = spawn('taskkill', ['/pid', String(serverProcess.pid), '/T', '/F'], {
        stdio: 'ignore',
      });

      killer.on('exit', resolve);
      killer.on('error', resolve);
    });
    return;
  }

  try {
    process.kill(-serverProcess.pid, 'SIGTERM');
  } catch {
    // The server may already have exited between the exit-code check and kill.
  }
}

function delay(milliseconds) {
  return new Promise((resolve) => setTimeout(resolve, milliseconds));
}
