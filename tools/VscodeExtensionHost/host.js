"use strict";

const fs = require("fs");
const path = require("path");
const { JsonRpcConnection } = require("./jsonrpc");

function parseArgs(argv) {
  const extensions = [];
  for (let i = 0; i < argv.length; i += 1) {
    const arg = argv[i];
    if (arg === "--extension" && i + 1 < argv.length) {
      extensions.push(argv[i + 1]);
      i += 1;
    }
  }

  return { extensions };
}

class Disposable {
  constructor(dispose) {
    this._dispose = dispose;
    this._isDisposed = false;
  }

  dispose() {
    if (this._isDisposed) {
      return;
    }
    this._isDisposed = true;
    if (this._dispose) {
      this._dispose();
    }
  }

  static from(...items) {
    return new Disposable(() => items.forEach((item) => item.dispose()));
  }
}

class EventEmitter {
  constructor() {
    this._listeners = new Set();
    this.event = (listener) => {
      this._listeners.add(listener);
      return new Disposable(() => this._listeners.delete(listener));
    };
  }

  fire(value) {
    for (const listener of this._listeners) {
      try {
        listener(value);
      } catch (err) {
      }
    }
  }
}

class Memento {
  constructor() {
    this._store = new Map();
  }

  get(key, defaultValue) {
    return this._store.has(key) ? this._store.get(key) : defaultValue;
  }

  update(key, value) {
    this._store.set(key, value);
    return Promise.resolve();
  }
}

class Uri {
  constructor(fsPath) {
    this.fsPath = fsPath;
  }

  toString() {
    return this.fsPath;
  }

  static file(filePath) {
    return new Uri(filePath);
  }
}

const args = parseArgs(process.argv.slice(2));
if (args.extensions.length === 0) {
  console.error("No extensions specified.");
  process.exit(2);
}

const connection = new JsonRpcConnection(process.stdin, process.stdout);
const commandHandlers = new Map();
const deactivateHandlers = [];

connection.onNotification("vscode.commands.invoke", async (params) => {
  if (!params || !params.id) {
    return;
  }

  const handler = commandHandlers.get(params.id);
  if (!handler) {
    return;
  }

  const argsList = Array.isArray(params.args) ? params.args : [];
  try {
    await handler(...argsList);
  } catch (err) {
    const text = err && err.message ? err.message : String(err);
    console.error(`Command ${params.id} failed: ${text}`);
  }
});

function createConfiguration(values) {
  return {
    get: (key, defaultValue) => {
      if (!values || typeof values !== "object") {
        return defaultValue;
      }
      if (Object.prototype.hasOwnProperty.call(values, key)) {
        return values[key];
      }
      return defaultValue;
    }
  };
}

function createVscodeApi() {
  return {
    commands: {
      registerCommand: (id, handler) => {
        commandHandlers.set(id, handler);
        connection.sendRequest("vscode.commands.register", { id }).catch((err) => {
          console.error(`registerCommand failed: ${err.message}`);
        });
        return new Disposable(() => commandHandlers.delete(id));
      },
      executeCommand: (id, ...cmdArgs) => {
        return connection.sendRequest("vscode.commands.execute", { id, args: cmdArgs });
      },
      getCommands: () => {
        return connection
          .sendRequest("vscode.commands.get", {})
          .then((result) => (result && result.commands ? result.commands : []));
      }
    },
    window: {
      showInformationMessage: (text) => {
        return connection.sendRequest("vscode.window.showInformationMessage", { text });
      },
      showWarningMessage: (text) => {
        return connection.sendRequest("vscode.window.showWarningMessage", { text });
      },
      showErrorMessage: (text) => {
        return connection.sendRequest("vscode.window.showErrorMessage", { text });
      }
    },
    workspace: {
      getConfiguration: (section) => {
        return connection
          .sendRequest("vscode.workspace.getConfiguration", { section })
          .then((result) => createConfiguration(result.values));
      }
    },
    EventEmitter,
    Disposable,
    Uri
  };
}

async function activateExtension(extensionPath) {
  const packagePath = path.join(extensionPath, "package.json");
  const raw = fs.readFileSync(packagePath, "utf8");
  const pkg = JSON.parse(raw);
  const mainFile = pkg.main ? path.join(extensionPath, pkg.main) : path.join(extensionPath, "index.js");
  const mod = require(mainFile);

  if (mod && typeof mod.activate === "function") {
    const context = {
      subscriptions: [],
      extensionPath,
      globalState: new Memento(),
      workspaceState: new Memento(),
      asAbsolutePath: (relativePath) => path.join(extensionPath, relativePath)
    };

    const result = mod.activate(context);
    if (result && typeof result.then === "function") {
      await result;
    }
  }

  if (mod && typeof mod.deactivate === "function") {
    deactivateHandlers.push(mod.deactivate);
  }
}

async function start() {
  global.__xveVscodeApi = createVscodeApi();
  connection.start();

  for (const extensionPath of args.extensions) {
    try {
      await activateExtension(extensionPath);
    } catch (err) {
      const text = err && err.message ? err.message : String(err);
      console.error(`Failed to activate ${extensionPath}: ${text}`);
    }
  }
}

async function shutdown() {
  for (const handler of deactivateHandlers) {
    try {
      const result = handler();
      if (result && typeof result.then === "function") {
        await result;
      }
    } catch (err) {
    }
  }
}

process.on("SIGTERM", () => {
  shutdown().finally(() => process.exit(0));
});

process.on("SIGINT", () => {
  shutdown().finally(() => process.exit(0));
});

start().catch((err) => {
  console.error(err && err.message ? err.message : String(err));
  process.exit(1);
});
