"use strict";

class JsonRpcConnection {
  constructor(input, output) {
    this._input = input;
    this._output = output;
    this._buffer = Buffer.alloc(0);
    this._pending = new Map();
    this._nextId = 1;
    this._requestHandlers = new Map();
    this._notificationHandlers = new Map();
  }

  start() {
    this._input.on("data", (chunk) => this._onData(chunk));
  }

  onRequest(method, handler) {
    this._requestHandlers.set(method, handler);
  }

  onNotification(method, handler) {
    this._notificationHandlers.set(method, handler);
  }

  sendRequest(method, params) {
    const id = this._nextId++;
    const payload = { jsonrpc: "2.0", id, method, params };
    const promise = new Promise((resolve, reject) => {
      this._pending.set(id, { resolve, reject });
    });

    this._sendMessage(payload);
    return promise;
  }

  sendNotification(method, params) {
    const payload = { jsonrpc: "2.0", method, params };
    this._sendMessage(payload);
  }

  _sendMessage(payload) {
    const body = Buffer.from(JSON.stringify(payload), "utf8");
    const header = `Content-Length: ${body.length}\r\n\r\n`;
    this._output.write(header, "ascii");
    this._output.write(body);
  }

  async _handleMessage(message) {
    if (message && message.method) {
      if (message.id !== undefined) {
        const handler = this._requestHandlers.get(message.method);
        if (!handler) {
          this._sendMessage({
            jsonrpc: "2.0",
            id: message.id,
            error: { code: -32601, message: "Method not found" }
          });
          return;
        }

        try {
          const result = await handler(message.params);
          this._sendMessage({ jsonrpc: "2.0", id: message.id, result });
        } catch (err) {
          const text = err && err.message ? err.message : "Unhandled error";
          this._sendMessage({
            jsonrpc: "2.0",
            id: message.id,
            error: { code: -32000, message: text }
          });
        }

        return;
      }

      const notification = this._notificationHandlers.get(message.method);
      if (notification) {
        notification(message.params);
      }

      return;
    }

    if (message && message.id !== undefined) {
      const pending = this._pending.get(message.id);
      if (pending) {
        this._pending.delete(message.id);
        if (message.error) {
          pending.reject(new Error(message.error.message || "Request failed"));
        } else {
          pending.resolve(message.result);
        }
      }
    }
  }

  _onData(chunk) {
    this._buffer = Buffer.concat([this._buffer, chunk]);
    while (true) {
      const headerEnd = this._buffer.indexOf("\r\n\r\n");
      if (headerEnd === -1) {
        return;
      }

      const headerText = this._buffer.slice(0, headerEnd).toString("ascii");
      const match = /Content-Length:\s*(\d+)/i.exec(headerText);
      if (!match) {
        this._buffer = Buffer.alloc(0);
        return;
      }

      const length = parseInt(match[1], 10);
      const bodyStart = headerEnd + 4;
      const bodyEnd = bodyStart + length;
      if (this._buffer.length < bodyEnd) {
        return;
      }

      const body = this._buffer.slice(bodyStart, bodyEnd).toString("utf8");
      this._buffer = this._buffer.slice(bodyEnd);

      let message = null;
      try {
        message = JSON.parse(body);
      } catch (err) {
        continue;
      }

      this._handleMessage(message);
    }
  }
}

module.exports = { JsonRpcConnection };
