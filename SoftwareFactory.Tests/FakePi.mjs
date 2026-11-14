// Deterministic Pi RPC stand-in: no credentials, model calls, or network access.
import fs from "node:fs";

const startRecord = { args: process.argv.slice(2), cwd: process.cwd(), pid: process.pid };
fs.writeFileSync("harness-start.json", JSON.stringify(startRecord));
fs.appendFileSync("harness-starts.jsonl", JSON.stringify(startRecord) + "\n");
const send = (record) => process.stdout.write(JSON.stringify(record) + "\n");
const response = (command, data = {}) => send({ id: command.id, type: "response", command: command.type, success: true, data });
const assistant = (text, stopReason = "stop", errorMessage) => send({
    type: "message_end", message: { role: "assistant", content: [{ type: "text", text }], stopReason, errorMessage }
});
let promptCount = 0;
let pendingUi;
let input = "";
let ignoreShutdown = false;

process.stdin.setEncoding("utf8");
process.stdin.on("data", (chunk) => {
    input += chunk;
    let boundary;
    while ((boundary = input.indexOf("\n")) !== -1) {
        const line = input.slice(0, boundary).replace(/\r$/, "");
        input = input.slice(boundary + 1);
        if (line) handle(JSON.parse(line));
    }
});
process.stdin.on("end", () => {
    if (ignoreShutdown) setInterval(() => {}, 1000);
    else process.exit(0);
});

function handle(command) {
    fs.appendFileSync("harness-commands.jsonl", JSON.stringify(command) + "\n");
    switch (command.type) {
        case "get_state":
            response(command, { sessionId: "fake-session", sessionFile: process.cwd() + "/session.jsonl" });
            break;
        case "set_model":
        case "set_thinking_level":
            send({ type: "queue_update", steering: [], followUp: [] });
            response(command);
            break;
        case "abort":
            response(command);
            break;
        case "extension_ui_response":
            if (!command.cancelled) throw new Error("Unattended dialogs must be cancelled");
            assistant("Dialog cancelled safely");
            send({ type: "agent_settled", aborted: false });
            response(pendingUi, { disposition: "started" });
            break;
        case "prompt": {
            promptCount++;
            if (command.message === "reject") {
                send({ id: command.id, type: "response", command: "prompt", success: false, error: "Prompt rejected" });
                return;
            }
            if (command.message === "crash") {
                process.stderr.write("Simulated harness crash\n");
                setTimeout(() => process.exit(7), 20);
                return;
            }
            if (command.message === "ignore") return;
            if (command.message === "handled") {
                response(command, { disposition: "handled" });
                return;
            }
            if (command.message === "hang" || command.message === "hang-shutdown") {
                ignoreShutdown = command.message === "hang-shutdown";
                response(command, { disposition: "started" });
                return;
            }
            if (command.message === "ui") {
                pendingUi = command;
                send({ type: "extension_ui_request", id: "dialog-1", method: "confirm", title: "Approve something?" });
                return;
            }
            if (command.message === "retry") {
                assistant("", "error", "Temporary provider failure");
                send({ type: "agent_end", messages: [], willRetry: true });
                send({ type: "auto_retry_start", attempt: 1, maxAttempts: 3, delayMs: 1 });
                send({ type: "auto_retry_end", success: true, attempt: 1 });
            }
            send({ type: "agent_start" });
            send({ type: "message_update", assistantMessageEvent: { type: "text_delta", contentIndex: 0, delta: "Cześć \u2028\u2029" } });
            if (command.message === "error") assistant("", "error", "Provider unavailable");
            else assistant(`Reply ${promptCount}: ${command.message}`);
            send({ type: "agent_end", messages: [], willRetry: false });
            if (command.message === "delayed") {
                response(command, { disposition: "started" });
                setTimeout(() => {
                    assistant("Settled after agent_end");
                    send({ type: "agent_settled", aborted: false });
                }, 80);
            } else {
                // Intentionally finish BEFORE acknowledging prompt acceptance.
                send({ type: "agent_settled", aborted: command.message === "aborted" });
                response(command, { disposition: "started" });
            }
            break;
        }
        default: throw new Error("Unexpected RPC command: " + command.type);
    }
}
