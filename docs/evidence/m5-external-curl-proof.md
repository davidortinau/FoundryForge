# M5 — external curl proof (SC-004)

Hardware: real Apple Silicon, net11.0-macos. The exposed Foundry Local OpenAI-compatible
server was toggled ON from the FoundryStudio Server panel (bound URL reported verbatim by the
UI from `manager.Urls`). An **out-of-process** `curl` then hit the endpoint.

## Server running → bound URL
`http://127.0.0.1:5273` (shown in the Server panel; pilot light lit; status "running")

## GET /v1/models (external)
    $ curl http://127.0.0.1:5273/v1/models
    {"data":[{"id":"qwen2.5-0.5b-instruct-generic-gpu","owned_by":"Microsoft", ... }], "object":"list"}

## POST /v1/chat/completions (external) — the defining proof
    $ curl http://127.0.0.1:5273/v1/chat/completions -H "Content-Type: application/json" \
        -d '{"model":"qwen2.5-0.5b","messages":[{"role":"user","content":"Reply with exactly: hello from the server"}],"max_tokens":40}'
    {"model":"qwen2.5-0.5b","choices":[{"message":{"role":"assistant",
     "content":"Hello! It seems like you've just clicked on this message to find out who I am ..."},
     "finish_reason":"stop"}],
     "usage":{"prompt_tokens":37,"completion_tokens":32,"total_tokens":69}, "object":"chat.completion"}

(Before the model was loaded, the server honestly returned: "Model 'qwen2.5-0.5b' is not loaded.
Please load the model before getting a ChatClient." — no fabrication.)

## Server stopped → connection refused
After toggling the server OFF (status "stopped", pilot unlit, endpoint gone), the same curl:
    $ curl http://127.0.0.1:5273/v1/models   →   http_code 000 (connection refused)

Honesty verified: no port/auth/LAN controls on the panel; the bound URL is the verbatim `Urls`
value (not a fabricated/assumed address); the request log is honestly omitted (FL exposes no
live feed). The server is for EXTERNAL tools only — in-app chat runs in-process and is
unaffected by the server's on/off state.
