"use client";

import { useState, useEffect, useId } from "react";
import {
  Copy,
  Check,
  Eye,
  EyeOff,
  Terminal,
  Key,
  ShieldCheck,
  Zap,
  Send,
  Radio,
  Clock,
  CheckCircle2,
  RefreshCw,
  Sparkles,
  Layers,
  ArrowRight
} from "lucide-react";

type WebhookEventType =
  | "charge.complete"
  | "qr.paid"
  | "refund.created"
  | "payout.settled"
  | "dispute.opened";

interface DeliveryLog {
  id: string;
  eventType: WebhookEventType;
  timestamp: string;
  statusCode: number;
  durationMs: number;
  endpointUrl: string;
  signature: string;
  responseBody: string;
}

export default function DevelopersPage() {
  const [copiedKey, setCopiedKey] = useState<string | null>(null);
  const [showSecret, setShowSecret] = useState(false);

  // Webhook Tester State
  const [selectedEvent, setSelectedEvent] = useState<WebhookEventType>("charge.complete");
  const [endpointUrl, setEndpointUrl] = useState("https://merchant.example.com/api/webhooks/payflow");
  const [webhookSecret, setWebhookSecret] = useState("whsec_live_9481a8bf9241517726a");
  const [simulatedStatus, setSimulatedStatus] = useState<200 | 500>(200);
  const [isSending, setIsSending] = useState(false);
  const [liveSignature, setLiveSignature] = useState("");
  const [currentTimestamp, setCurrentTimestamp] = useState<number>(Math.floor(Date.now() / 1000));
  const [lastDelivery, setLastDelivery] = useState<DeliveryLog | null>(null);
  const [deliveryLogs, setDeliveryLogs] = useState<DeliveryLog[]>([]);

  const apiKey = "pk_live_payflow_9481a8bf9241";
  const apiSecret = "sk_live_payflow_sec_9941088219481";

  const handleCopy = (text: string, keyName: string) => {
    navigator.clipboard.writeText(text);
    setCopiedKey(keyName);
    setTimeout(() => setCopiedKey(null), 2000);
  };

  // Generate dynamic sample payload based on event type
  const getPayloadForEvent = (event: WebhookEventType, ts: number) => {
    const isoDate = new Date(ts * 1000).toISOString();

    switch (event) {
      case "charge.complete":
        return {
          id: `evt_charge_${ts}_001`,
          object: "event",
          type: "charge.complete",
          created_at: isoDate,
          api_version: "2026-09-01",
          data: {
            object: {
              id: "chrg_live_998124501",
              merchant_id: "11111111-1111-1111-1111-111111111111",
              amount: 250000,
              currency: "THB",
              status: "successful",
              paid_at: isoDate,
              payment_method: {
                type: "credit_card",
                brand: "visa",
                last4: "4242",
                funding: "credit",
                issuer: "Kasikornbank (KBANK)"
              },
              routing: {
                primary_gateway: "opn",
                fallback_attempted: false,
                latency_ms: 284
              },
              metadata: {
                order_id: "ORD-99201",
                customer_email: "somchai@promptpay.in.th"
              }
            }
          }
        };

      case "qr.paid":
        return {
          id: `evt_qr_${ts}_002`,
          object: "event",
          type: "qr.paid",
          created_at: isoDate,
          api_version: "2026-09-01",
          data: {
            object: {
              id: "qr_scb_819201948",
              merchant_id: "11111111-1111-1111-1111-111111111111",
              amount: 45000,
              currency: "THB",
              status: "successful",
              qr_type: "promptpay_dynamic",
              biller_id: "010753600010201",
              reference1: "REF990141",
              paid_via: "SCB Easy App",
              settlement_cycle: "T+0_instant",
              metadata: {
                order_id: "ORD-QR-7712"
              }
            }
          }
        };

      case "refund.created":
        return {
          id: `evt_ref_${ts}_003`,
          object: "event",
          type: "refund.created",
          created_at: isoDate,
          api_version: "2026-09-01",
          data: {
            object: {
              id: "rfnd_live_55102914",
              charge_id: "chrg_live_998124501",
              amount: 50000,
              currency: "THB",
              status: "succeeded",
              reason: "customer_return",
              gateway_ref: "omise_rfnd_992"
            }
          }
        };

      case "payout.settled":
        return {
          id: `evt_po_${ts}_004`,
          object: "event",
          type: "payout.settled",
          created_at: isoDate,
          api_version: "2026-09-01",
          data: {
            object: {
              id: "po_bbl_4910284",
              amount: 42000000,
              currency: "THB",
              status: "paid",
              destination_bank: "Bangkok Bank (BBL)",
              account_last4: "8821",
              cleared_batches: ["batch_20260911_01"]
            }
          }
        };

      case "dispute.opened":
        return {
          id: `evt_disp_${ts}_005`,
          object: "event",
          type: "dispute.opened",
          created_at: isoDate,
          api_version: "2026-09-01",
          data: {
            object: {
              id: "dp_visa_7718290",
              charge_id: "chrg_live_998124501",
              amount: 250000,
              currency: "THB",
              status: "under_review",
              reason: "unrecognized_transaction",
              evidence_due_by: "2026-09-26T23:59:59.000Z"
            }
          }
        };
    }
  };

  const currentPayloadObj = getPayloadForEvent(selectedEvent, currentTimestamp);
  const currentPayloadJson = JSON.stringify(currentPayloadObj, null, 2);

  // Compute HMAC SHA-256 signature in browser
  useEffect(() => {
    let isMounted = true;

    async function generateHmac() {
      const payloadString = currentPayloadJson;
      try {
        if (typeof window !== "undefined" && window.crypto && window.crypto.subtle) {
          const enc = new TextEncoder();
          const keyData = enc.encode(webhookSecret);
          const cryptoKey = await window.crypto.subtle.importKey(
            "raw",
            keyData,
            { name: "HMAC", hash: "SHA-256" },
            false,
            ["sign"]
          );
          const signature = await window.crypto.subtle.sign(
            "HMAC",
            cryptoKey,
            enc.encode(payloadString)
          );
          const hashHex = Array.from(new Uint8Array(signature))
            .map((b) => b.toString(16).padStart(2, "0"))
            .join("");

          if (isMounted) {
            setLiveSignature(`t=${currentTimestamp},v1=${hashHex}`);
          }
        } else {
          // Fallback hash
          setLiveSignature(`t=${currentTimestamp},v1=e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`);
        }
      } catch (err) {
        console.error("HMAC calculation error:", err);
      }
    }

    generateHmac();
    return () => {
      isMounted = false;
    };
  }, [currentPayloadJson, webhookSecret, currentTimestamp]);

  const handleSendMockWebhook = () => {
    setIsSending(true);

    const nowTs = Math.floor(Date.now() / 1000);
    setCurrentTimestamp(nowTs);

    // Simulate realistic network roundtrip
    setTimeout(() => {
      const duration = Math.floor(Math.random() * 35) + 25; // 25-60ms
      const deliveryId = `deliv_${Date.now()}`;

      const responseBody =
        simulatedStatus === 200
          ? JSON.stringify(
              {
                received: true,
                event: selectedEvent,
                status: "acknowledged",
                processed_at: new Date().toISOString()
              },
              null,
              2
            )
          : JSON.stringify(
              {
                error: "InternalServerError",
                message: "Merchant destination server returned 500. PayFlow retry scheduled in 60s."
              },
              null,
              2
            );

      const log: DeliveryLog = {
        id: deliveryId,
        eventType: selectedEvent,
        timestamp: "Just now",
        statusCode: simulatedStatus,
        durationMs: duration,
        endpointUrl: endpointUrl,
        signature: liveSignature,
        responseBody
      };

      setLastDelivery(log);
      setDeliveryLogs((prev) => [log, ...prev.slice(0, 4)]);
      setIsSending(false);
    }, 450);
  };

  const curlExample = `curl -X POST https://api.payflow.io/api/v1/payments \\
  -H "Content-Type: application/json" \\
  -H "X-Merchant-Id: 11111111-1111-1111-1111-111111111111" \\
  -H "Idempotency-Key: idemp_${Date.now()}" \\
  -d '{
    "merchantReference": "ORD-10029",
    "amount": 1500.00,
    "currency": "THB",
    "paymentMethod": 2
  }'`;

  const webhookCurlExample = `curl -X POST "${endpointUrl}" \\
  -H "Content-Type: application/json" \\
  -H "X-PayFlow-Signature: ${liveSignature || "t=...,v1=..."}" \\
  -H "X-PayFlow-Event: ${selectedEvent}" \\
  -d '${JSON.stringify(currentPayloadObj)}'`;

  return (
    <div className="space-y-8 max-w-7xl mx-auto">
      <div>
        <h1 className="text-2xl font-bold text-white tracking-tight">Developer Integration & API Keys</h1>
        <p className="text-sm text-slate-400">
          One API endpoint connects all payment gateways with automatic failover and cryptographically signed webhooks.
        </p>
      </div>

      {/* API Keys Card */}
      <div className="p-6 bg-slate-900/40 border border-slate-800 rounded-2xl space-y-4">
        <div className="flex items-center gap-2 text-white font-semibold text-sm">
          <Key className="w-4 h-4 text-indigo-400" />
          <span>Merchant API Credentials</span>
        </div>

        <div className="space-y-3">
          <div>
            <label className="block text-xs text-slate-400 mb-1">Publishable Key (Client-side / Web)</label>
            <div className="flex items-center gap-2">
              <input
                type="text"
                readOnly
                value={apiKey}
                className="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs font-mono text-slate-300 focus:outline-none"
              />
              <button
                onClick={() => handleCopy(apiKey, "pubKey")}
                className="px-3 py-2 bg-slate-800 hover:bg-slate-700 text-slate-200 rounded-lg text-xs flex items-center gap-1.5 transition-colors"
              >
                {copiedKey === "pubKey" ? <Check className="w-3.5 h-3.5 text-emerald-400" /> : <Copy className="w-3.5 h-3.5" />}
                <span>{copiedKey === "pubKey" ? "Copied" : "Copy"}</span>
              </button>
            </div>
          </div>

          <div>
            <label className="block text-xs text-slate-400 mb-1">Secret Key (Server-to-Server Only)</label>
            <div className="flex items-center gap-2">
              <input
                type={showSecret ? "text" : "password"}
                readOnly
                value={apiSecret}
                className="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs font-mono text-slate-300 focus:outline-none"
              />
              <button
                onClick={() => setShowSecret(!showSecret)}
                className="p-2 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded-lg text-xs"
                title={showSecret ? "Hide Secret" : "Show Secret"}
              >
                {showSecret ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
              </button>
              <button
                onClick={() => handleCopy(apiSecret, "secKey")}
                className="px-3 py-2 bg-slate-800 hover:bg-slate-700 text-slate-200 rounded-lg text-xs flex items-center gap-1.5 transition-colors"
              >
                {copiedKey === "secKey" ? <Check className="w-3.5 h-3.5 text-emerald-400" /> : <Copy className="w-3.5 h-3.5" />}
                <span>{copiedKey === "secKey" ? "Copied" : "Copy"}</span>
              </button>
            </div>
          </div>
        </div>
      </div>

      {/* Interactive Webhook Tester / Simulation Console */}
      <div className="p-6 bg-slate-900/40 border border-indigo-500/30 rounded-2xl space-y-6 shadow-lg shadow-indigo-950/20">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b border-slate-800 pb-4">
          <div className="flex items-center gap-2.5">
            <div className="p-2 bg-indigo-500/10 rounded-xl text-indigo-400 border border-indigo-500/20">
              <Zap className="w-5 h-5 text-indigo-400" />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <h2 className="text-base font-bold text-white">Interactive Webhook Simulator Console</h2>
                <span className="px-2 py-0.5 rounded-full text-[10px] font-bold bg-indigo-500/20 text-indigo-300 border border-indigo-500/30 uppercase">
                  Live Tester
                </span>
              </div>
              <p className="text-xs text-slate-400">
                Trigger real-time webhook payloads with live HMAC SHA-256 signatures to verify your endpoint handler.
              </p>
            </div>
          </div>

          {/* Quick status toggle for error testing */}
          <div className="flex items-center gap-2 text-xs">
            <span className="text-slate-400">Simulate Response:</span>
            <button
              onClick={() => setSimulatedStatus(200)}
              className={`px-2.5 py-1 rounded-lg font-semibold transition-all ${
                simulatedStatus === 200
                  ? "bg-emerald-500/20 text-emerald-300 border border-emerald-500/40"
                  : "bg-slate-950 text-slate-400 border border-slate-800"
              }`}
            >
              200 OK
            </button>
            <button
              onClick={() => setSimulatedStatus(500)}
              className={`px-2.5 py-1 rounded-lg font-semibold transition-all ${
                simulatedStatus === 500
                  ? "bg-rose-500/20 text-rose-300 border border-rose-500/40"
                  : "bg-slate-950 text-slate-400 border border-slate-800"
              }`}
            >
              500 Error
            </button>
          </div>
        </div>

        {/* Configuration Row */}
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div>
            <label className="block text-xs font-semibold text-slate-300 mb-1.5 flex items-center justify-between">
              <span>Event Type</span>
              <span className="text-[11px] text-slate-400">PayFlow Webhooks v2</span>
            </label>
            <div className="grid grid-cols-2 sm:grid-cols-3 gap-1.5">
              {(
                [
                  { id: "charge.complete", label: "charge.complete", desc: "Card Payment" },
                  { id: "qr.paid", label: "qr.paid", desc: "PromptPay QR" },
                  { id: "refund.created", label: "refund.created", desc: "Refund Event" },
                  { id: "payout.settled", label: "payout.settled", desc: "Bank Payout" },
                  { id: "dispute.opened", label: "dispute.opened", desc: "Dispute Flag" }
                ] as const
              ).map((evt) => (
                <button
                  key={evt.id}
                  onClick={() => {
                    setSelectedEvent(evt.id);
                    setCurrentTimestamp(Math.floor(Date.now() / 1000));
                  }}
                  className={`p-2 rounded-xl text-left border transition-all ${
                    selectedEvent === evt.id
                      ? "bg-indigo-600/20 border-indigo-500 text-white shadow-sm shadow-indigo-600/20"
                      : "bg-slate-950 border-slate-800/80 text-slate-400 hover:border-slate-700 hover:text-slate-200"
                  }`}
                >
                  <div className="font-mono text-xs font-bold leading-none mb-1 text-indigo-300">{evt.label}</div>
                  <div className="text-[10px] text-slate-400 truncate">{evt.desc}</div>
                </button>
              ))}
            </div>
          </div>

          <div className="space-y-3">
            <div>
              <label className="block text-xs font-semibold text-slate-300 mb-1.5">
                Destination Webhook Endpoint URL
              </label>
              <input
                type="text"
                value={endpointUrl}
                onChange={(e) => setEndpointUrl(e.target.value)}
                placeholder="https://yourdomain.com/api/webhooks"
                className="w-full bg-slate-950 border border-slate-800 rounded-xl px-3 py-2 text-xs font-mono text-slate-200 focus:outline-none focus:border-indigo-500 transition-colors"
              />
            </div>

            <div>
              <label className="block text-xs font-semibold text-slate-300 mb-1.5 flex items-center justify-between">
                <span>Webhook Signing Secret</span>
                <span className="text-[10px] text-emerald-400 flex items-center gap-1 font-normal">
                  <ShieldCheck className="w-3 h-3" /> HMAC SHA-256 Enabled
                </span>
              </label>
              <input
                type="text"
                value={webhookSecret}
                onChange={(e) => setWebhookSecret(e.target.value)}
                className="w-full bg-slate-950 border border-slate-800 rounded-xl px-3 py-2 text-xs font-mono text-slate-300 focus:outline-none focus:border-indigo-500 transition-colors"
              />
            </div>
          </div>
        </div>

        {/* Live Headers & HMAC Signature Banner */}
        <div className="bg-slate-950 p-4 rounded-xl border border-slate-800/80 space-y-2.5">
          <div className="flex items-center justify-between">
            <span className="text-[11px] uppercase tracking-wider text-slate-400 font-semibold flex items-center gap-1.5">
              <ShieldCheck className="w-3.5 h-3.5 text-indigo-400" />
              Generated Request Headers
            </span>
            <button
              onClick={() => handleCopy(liveSignature, "sig")}
              className="text-xs text-indigo-400 hover:text-indigo-300 flex items-center gap-1"
            >
              {copiedKey === "sig" ? <Check className="w-3 h-3 text-emerald-400" /> : <Copy className="w-3 h-3" />}
              <span>{copiedKey === "sig" ? "Copied" : "Copy Signature"}</span>
            </button>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-2 text-xs font-mono">
            <div className="p-2 bg-slate-900/60 rounded-lg border border-slate-800">
              <span className="text-slate-400 block text-[10px]">Content-Type</span>
              <span className="text-slate-200">application/json</span>
            </div>
            <div className="p-2 bg-slate-900/60 rounded-lg border border-slate-800">
              <span className="text-slate-400 block text-[10px]">X-PayFlow-Event</span>
              <span className="text-indigo-300 font-semibold">{selectedEvent}</span>
            </div>
            <div className="p-2 bg-slate-900/60 rounded-lg border border-slate-800 col-span-1 md:col-span-2 overflow-x-auto">
              <span className="text-slate-400 block text-[10px]">X-PayFlow-Signature (HMAC SHA-256)</span>
              <span className="text-amber-300 break-all select-all">{liveSignature || "Generating signature..."}</span>
            </div>
          </div>
        </div>

        {/* Live Payload Preview */}
        <div className="space-y-2">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <span className="text-xs font-semibold text-slate-300">Live JSON Payload</span>
              <span className="text-[10px] text-slate-400 font-mono">({selectedEvent})</span>
            </div>
            <div className="flex items-center gap-2">
              <button
                onClick={() => handleCopy(webhookCurlExample, "curlWebhook")}
                className="text-xs px-2.5 py-1 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded-lg flex items-center gap-1 transition-colors"
              >
                {copiedKey === "curlWebhook" ? <Check className="w-3 h-3 text-emerald-400" /> : <Terminal className="w-3 h-3" />}
                <span>{copiedKey === "curlWebhook" ? "Copied cURL" : "Copy cURL"}</span>
              </button>
              <button
                onClick={() => handleCopy(currentPayloadJson, "payload")}
                className="text-xs px-2.5 py-1 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded-lg flex items-center gap-1 transition-colors"
              >
                {copiedKey === "payload" ? <Check className="w-3 h-3 text-emerald-400" /> : <Copy className="w-3 h-3" />}
                <span>{copiedKey === "payload" ? "Copied" : "Copy JSON"}</span>
              </button>
            </div>
          </div>

          <pre className="p-4 bg-slate-950 rounded-xl border border-slate-800 font-mono text-xs text-slate-300 overflow-x-auto max-h-64 leading-relaxed">
            <code>{currentPayloadJson}</code>
          </pre>
        </div>

        {/* Action Button & Dispatch State */}
        <div className="flex flex-col sm:flex-row items-center justify-between gap-4 pt-2">
          <div className="text-xs text-slate-400 flex items-center gap-2">
            <Clock className="w-3.5 h-3.5 text-slate-400" />
            <span>Timestamp unix: </span>
            <span className="font-mono text-white font-semibold">{currentTimestamp}</span>
          </div>

          <button
            onClick={handleSendMockWebhook}
            disabled={isSending}
            className="w-full sm:w-auto px-6 py-2.5 bg-indigo-600 hover:bg-indigo-500 disabled:opacity-50 text-white rounded-xl text-xs font-bold shadow-lg shadow-indigo-600/30 transition-all flex items-center justify-center gap-2"
          >
            {isSending ? (
              <>
                <RefreshCw className="w-4 h-4 animate-spin" />
                <span>Simulating Dispatch...</span>
              </>
            ) : (
              <>
                <Send className="w-4 h-4" />
                <span>Send Mock Webhook</span>
              </>
            )}
          </button>
        </div>

        {/* Simulation Delivery Result */}
        {lastDelivery && (
          <div
            className={`p-4 rounded-xl border transition-all animate-in fade-in-50 duration-200 ${
              lastDelivery.statusCode === 200
                ? "bg-emerald-950/30 border-emerald-500/30"
                : "bg-rose-950/30 border-rose-500/30"
            }`}
          >
            <div className="flex items-center justify-between mb-3">
              <div className="flex items-center gap-2">
                <span
                  className={`px-2 py-0.5 rounded text-xs font-bold font-mono ${
                    lastDelivery.statusCode === 200
                      ? "bg-emerald-500/20 text-emerald-300 border border-emerald-500/40"
                      : "bg-rose-500/20 text-rose-300 border border-rose-500/40"
                  }`}
                >
                  HTTP {lastDelivery.statusCode} {lastDelivery.statusCode === 200 ? "OK" : "ERROR"}
                </span>
                <span className="text-xs font-semibold text-white">
                  Event Delivered ({lastDelivery.eventType})
                </span>
                <span className="text-[11px] text-slate-400 font-mono">
                  {lastDelivery.durationMs}ms latency
                </span>
              </div>
              <span className="text-xs text-slate-400">{lastDelivery.timestamp}</span>
            </div>

            <div className="space-y-2">
              <div className="text-[11px] text-slate-400">Response Body Received:</div>
              <pre className="p-3 bg-slate-950/80 rounded-lg border border-slate-800/80 font-mono text-xs text-slate-300 overflow-x-auto">
                <code>{lastDelivery.responseBody}</code>
              </pre>
            </div>
          </div>
        )}

        {/* Recent Delivery History */}
        {deliveryLogs.length > 0 && (
          <div className="space-y-2 pt-2 border-t border-slate-800/80">
            <h3 className="text-xs font-semibold text-slate-400">Recent Webhook Dispatches</h3>
            <div className="space-y-1.5">
              {deliveryLogs.map((log) => (
                <div
                  key={log.id}
                  className="p-2.5 bg-slate-950 rounded-lg border border-slate-800/60 flex items-center justify-between text-xs"
                >
                  <div className="flex items-center gap-3">
                    <span
                      className={`px-1.5 py-0.5 rounded font-mono text-[10px] font-bold ${
                        log.statusCode === 200
                          ? "bg-emerald-500/20 text-emerald-400"
                          : "bg-rose-500/20 text-rose-400"
                      }`}
                    >
                      {log.statusCode}
                    </span>
                    <span className="font-mono text-white text-xs">{log.eventType}</span>
                    <span className="text-slate-400 truncate max-w-xs hidden sm:inline">
                      {log.endpointUrl}
                    </span>
                  </div>
                  <div className="flex items-center gap-3 text-slate-400">
                    <span className="font-mono text-[11px] text-emerald-400">{log.durationMs}ms</span>
                    <span>{log.timestamp}</span>
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>

      {/* Code Integration Snippet */}
      <div className="p-6 bg-slate-900/40 border border-slate-800 rounded-2xl space-y-4">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2 text-white font-semibold text-sm">
            <Terminal className="w-4 h-4 text-indigo-400" />
            <span>Process Payment Code Example</span>
          </div>
          <span className="text-xs text-slate-400 font-mono">cURL / REST</span>
        </div>

        <pre className="p-4 bg-slate-950 rounded-xl border border-slate-800/80 font-mono text-xs text-slate-300 overflow-x-auto">
          <code>{curlExample}</code>
        </pre>
      </div>
    </div>
  );
}

