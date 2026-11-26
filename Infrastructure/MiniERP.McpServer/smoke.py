"""Real stdio handshake and read-only calls; never prints business data."""
import json
import os
import pathlib
import queue
import subprocess
import threading

dll = pathlib.Path(__file__).parent / "bin/Release/net8.0/MiniERP.McpServer.dll"
if not dll.is_file():
    raise SystemExit("Build the MCP server in Release first.")
process = subprocess.Popen(["dotnet", str(dll)], stdin=subprocess.PIPE,
                           stdout=subprocess.PIPE, text=True, encoding="utf-8")
responses = queue.Queue()

def read_stdout():
    try:
        for line in process.stdout:
            responses.put(json.loads(line))
    except Exception as exc:
        responses.put(exc)
    finally:
        responses.put(EOFError("MCP server stopped"))

threading.Thread(target=read_stdout, daemon=True).start()

def send(message):
    process.stdin.write(json.dumps({"jsonrpc": "2.0", **message}) + "\n")
    process.stdin.flush()

def request(id_, method, params):
    send({"id": id_, "method": method, "params": params})
    while True:
        response = responses.get(timeout=30)
        if isinstance(response, Exception):
            raise response
        if response.get("id") == id_:
            if "error" in response:
                raise RuntimeError(f"{method}: protocol error")
            return response["result"]

def call(id_, name, arguments):
    result = request(id_, "tools/call", {"name": name, "arguments": arguments})
    assert not result.get("isError"), f"{name}: tool error"
    content = next(x["text"] for x in result["content"] if x["type"] == "text")
    return json.loads(content)

try:
    request(1, "initialize", {"protocolVersion": "2025-11-25",
                             "capabilities": {},
                             "clientInfo": {"name": "minierp-smoke", "version": "1.0"}})
    send({"method": "notifications/initialized"})
    tools = request(2, "tools/list", {})["tools"]
    assert {x["name"] for x in tools} == {
        "search_customers", "search_articles", "get_customer_quotations", "get_quotation"
    }
    assert all(x.get("annotations", {}).get("readOnlyHint") is True for x in tools)
    keyword = os.environ.get("MINIERP_SMOKE_KEYWORD", "MCP_SMOKE_NO_MATCH_7A88")
    assert "results" in call(3, "search_customers", {"keyword": keyword, "limit": 1})
    assert "results" in call(4, "search_articles", {"keyword": keyword, "limit": 1})
    assert "found" in call(5, "get_customer_quotations", {"customerId": 2147483647})
    assert "found" in call(6, "get_quotation", {"quotationId": 2147483647})
    print("PASS: MCP initialization, four read-only tools, database calls")
finally:
    process.stdin.close()
    try:
        process.wait(timeout=5)
    except subprocess.TimeoutExpired:
        process.kill()
        process.wait()
