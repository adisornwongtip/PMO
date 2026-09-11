"use client";

import { useState } from "react";
import { 
  Code2, 
  Terminal, 
  Copy, 
  Check, 
  Play, 
  ArrowRight, 
  ShieldCheck, 
  Key, 
  Zap, 
  ExternalLink,
  ChevronRight,
  BookOpen
} from "lucide-react";

interface Endpoint {
  id: string;
  name: string;
  method: "POST" | "GET" | "PUT" | "DELETE";
  path: string;
  description: string;
  category: "Payments & Routing" | "Reconciliation OS" | "Routing Rules" | "Telemetry";
  headers: { name: string; required: boolean; description: string; example: string }[];
  params: { name: string; type: string; required: boolean; description: string }[];
  curlCode: string;
  csharpCode: string;
  typescriptCode: string;
  sampleResponses: {
    label: string;
    status: number;
    payload: any;
  }[];
}

const endpoints: Endpoint[] = [
  {
    id: "process-payment",
    name: "Process Payment (Smart Routing)",
    method: "POST",
    path: "/api/v1/payments",
    category: "Payments & Routing",
    description: "Initiates a payment transaction. The orchestration engine dynamically selects the optimal provider (Opn, GB Prime Pay) based on rules, fee tiers, and real-time health, with zero-downtime automatic failover.",
    headers: [
      { name: "Content-Type", required: true, description: "Must be application/json", example: "application/json" },
      { name: "X-Merchant-Id", required: true, description: "Your unique merchant identifier", example: "11111111-1111-1111-1111-111111111111" },
      { name: "Idempotency-Key", required: true, description: "Unique UUID/string to prevent duplicate charges", example: "idemp_9481a8bf9241" },
    ],
    params: [
      { name: "merchantReference", type: "string", required: true, description: "Your internal order / transaction reference ID" },
      { name: "amount", type: "decimal", required: true, description: "Total payment amount in THB (e.g. 1500.00)" },
      { name: "currency", type: "string", required: false, description: "ISO 4217 Currency Code (default: THB)" },
      { name: "paymentMethod", type: "enum (int)", required: true, description: "1 = CreditCard, 2 = PromptPayQr, 3 = TrueMoney" },
      { name: "customerEmail", type: "string", required: false, description: "Customer email for receipts" },
    ],
    curlCode: `curl -X POST https://api.payflow.io/api/v1/payments \\
  -H "Content-Type: application/json" \\
  -H "X-Merchant-Id: 11111111-1111-1111-1111-111111111111" \\
  -H "Idempotency-Key: idemp_9481a8bf9241" \\
  -d '{
    "merchantReference": "ORD-94821",
    "amount": 1500.00,
    "currency": "THB",
    "paymentMethod": 2
  }'`,
    csharpCode: `using var client = new HttpClient();
client.DefaultRequestHeaders.Add("X-Merchant-Id", "11111111-1111-1111-1111-111111111111");
client.DefaultRequestHeaders.Add("Idempotency-Key", "idemp_9481a8bf9241");

var payload = new {
    merchantReference = "ORD-94821",
    amount = 1500.00m,
    currency = "THB",
    paymentMethod = 2
};

var response = await client.PostAsJsonAsync("https://api.payflow.io/api/v1/payments", payload);
var result = await response.Content.ReadFromJsonAsync<PaymentResponse>();`,
    typescriptCode: `const response = await fetch("https://api.payflow.io/api/v1/payments", {
  method: "POST",
  headers: {
    "Content-Type": "application/json",
    "X-Merchant-Id": "11111111-1111-1111-1111-111111111111",
    "Idempotency-Key": "idemp_9481a8bf9241"
  },
  body: JSON.stringify({
    merchantReference: "ORD-94821",
    amount: 1500.00,
    currency: "THB",
    paymentMethod: 2
  })
});
const data = await response.json();`,
    sampleResponses: [
      {
        label: "200 OK (Primary Opn Success)",
        status: 200,
        payload: {
          transactionId: "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          merchantReference: "ORD-94821",
          status: "Success",
          amount: 1500.00,
          currency: "THB",
          providerCode: "Opn",
          providerTransactionId: "chrg_opn_8317a94f01",
          qrCodeData: "00020101021229370016A000000677010111011300668999999995802TH53037645401500.005802TH6304",
          attemptCount: 1,
          completedAt: "2026-09-11T22:42:15.120Z"
        }
      },
      {
        label: "200 OK (Auto-Failover to GB Prime Pay)",
        status: 200,
        payload: {
          transactionId: "4fa85f64-5717-4562-b3fc-2c963f66afa7",
          merchantReference: "ORD-94822",
          status: "Success",
          amount: 4800.00,
          currency: "THB",
          providerCode: "GBPrimePay",
          providerTransactionId: "GB_20260911_4918",
          attemptCount: 2,
          completedAt: "2026-09-11T22:42:18.450Z"
        }
      },
      {
        label: "400 Bad Request (Card Declined)",
        status: 400,
        payload: {
          transactionId: "5fa85f64-5717-4562-b3fc-2c963f66afa8",
          merchantReference: "ORD-94823",
          status: "Failed",
          amount: 950.00,
          currency: "THB",
          providerCode: "Opn",
          failureReason: "Card declined: Insufficient funds (terminal error - no failover)",
          attemptCount: 1,
          completedAt: "2026-09-11T22:42:19.000Z"
        }
      }
    ]
  },
  {
    id: "get-transaction",
    name: "Get Transaction by ID",
    method: "GET",
    path: "/api/v1/payments/{id}",
    category: "Payments & Routing",
    description: "Retrieves complete transaction lifecycle including provider attempt history, latencies, and circuit breaker trace.",
    headers: [
      { name: "X-Merchant-Id", required: true, description: "Your merchant identifier", example: "11111111-1111-1111-1111-111111111111" }
    ],
    params: [
      { name: "id", type: "UUID", required: true, description: "The unique PayFlow transaction ID" }
    ],
    curlCode: `curl -X GET https://api.payflow.io/api/v1/payments/3fa85f64-5717-4562-b3fc-2c963f66afa6 \\
  -H "X-Merchant-Id: 11111111-1111-1111-1111-111111111111"`,
    csharpCode: `var txn = await client.GetFromJsonAsync<TransactionDetail>("https://api.payflow.io/api/v1/payments/{id}");`,
    typescriptCode: `const res = await fetch("https://api.payflow.io/api/v1/payments/" + id);
const txn = await res.json();`,
    sampleResponses: [
      {
        label: "200 OK",
        status: 200,
        payload: {
          id: "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          merchantReference: "ORD-94821",
          amount: 1500.00,
          status: "Success",
          selectedProvider: "Opn",
          routingLogs: [
            { providerCode: "Opn", statusCode: 200, latencyMs: 210, success: true }
          ]
        }
      }
    ]
  },
  {
    id: "upload-reconcile",
    name: "Upload Settlement Statement",
    method: "POST",
    path: "/api/v1/reconciliation/upload",
    category: "Reconciliation OS",
    description: "Uploads a batch of settlement records from an external gateway (Opn, GB Prime Pay, Bank Statement). Automatically audits and reconciles against PayFlow transaction ledgers.",
    headers: [
      { name: "Content-Type", required: true, description: "application/json", example: "application/json" },
      { name: "X-Merchant-Id", required: true, description: "Merchant identifier", example: "11111111-1111-1111-1111-111111111111" }
    ],
    params: [
      { name: "providerCode", type: "string", required: true, description: "Opn, GBPrimePay, TwoCTwoP" },
      { name: "fileName", type: "string", required: true, description: "Original statement file name" },
      { name: "records", type: "array", required: true, description: "Array of statement records with amount, fee, and settlement date" }
    ],
    curlCode: `curl -X POST https://api.payflow.io/api/v1/reconciliation/upload \\
  -H "Content-Type: application/json" \\
  -H "X-Merchant-Id: 11111111-1111-1111-1111-111111111111" \\
  -d '{
    "providerCode": "Opn",
    "fileName": "settlement_20260911.csv",
    "records": [
      {
        "providerTransactionId": "chrg_opn_8317a94f01",
        "merchantReference": "ORD-94821",
        "amount": 1500.00,
        "fee": 24.75,
        "netSettlement": 1475.25
      }
    ]
  }'`,
    csharpCode: `var response = await client.PostAsJsonAsync("https://api.payflow.io/api/v1/reconciliation/upload", uploadBatch);`,
    typescriptCode: `const res = await fetch("https://api.payflow.io/api/v1/reconciliation/upload", {
  method: "POST",
  body: JSON.stringify(uploadBatch)
});`,
    sampleResponses: [
      {
        label: "200 OK (Batch Reconciled)",
        status: 200,
        payload: {
          batchId: "9ba85f64-5717-4562-b3fc-2c963f66af99",
          providerCode: "Opn",
          fileName: "settlement_20260911.csv",
          totalRecords: 1,
          matchedCount: 1,
          unmatchedCount: 0,
          discrepancyCount: 0,
          totalSettledAmount: 1475.25
        }
      }
    ]
  },
  {
    id: "get-telemetry",
    name: "Dashboard Telemetry & Metrics",
    method: "GET",
    path: "/api/v1/dashboard/metrics",
    category: "Telemetry",
    description: "Returns aggregated gross volume (GMV), success rates, fee optimization savings, and provider-specific health metrics.",
    headers: [],
    params: [],
    curlCode: `curl -X GET https://api.payflow.io/api/v1/dashboard/metrics`,
    csharpCode: `var metrics = await client.GetFromJsonAsync<DashboardMetrics>("https://api.payflow.io/api/v1/dashboard/metrics");`,
    typescriptCode: `const res = await fetch("https://api.payflow.io/api/v1/dashboard/metrics");
const metrics = await res.json();`,
    sampleResponses: [
      {
        label: "200 OK",
        status: 200,
        payload: {
          gmv: 12845200.00,
          totalTransactions: 182421,
          successCount: 180122,
          failedCount: 2299,
          successRate: 98.74,
          estimatedFeesSaved: 44958.20,
          providers: [
            { provider: "Opn", successRate: 98.8, count: 112000 },
            { provider: "GBPrimePay", successRate: 99.2, count: 70421 }
          ]
        }
      }
    ]
  }
];

export default function ApiDocsPage() {
  const [selectedEndpoint, setSelectedEndpoint] = useState<Endpoint>(endpoints[0]);
  const [selectedLang, setSelectedLang] = useState<"curl" | "csharp" | "typescript">("curl");
  const [selectedResponseIdx, setSelectedResponseIdx] = useState<number>(0);
  const [copied, setCopied] = useState(false);
  const [isCalling, setIsCalling] = useState(false);
  const [liveCallSuccess, setLiveCallSuccess] = useState(false);

  const handleCopy = (text: string) => {
    navigator.clipboard.writeText(text);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  const handleTestCall = () => {
    setIsCalling(true);
    setTimeout(() => {
      setIsCalling(false);
      setLiveCallSuccess(true);
      setTimeout(() => setLiveCallSuccess(false), 3000);
    }, 600);
  };

  const getActiveCode = () => {
    switch (selectedLang) {
      case "csharp":
        return selectedEndpoint.csharpCode;
      case "typescript":
        return selectedEndpoint.typescriptCode;
      default:
        return selectedEndpoint.curlCode;
    }
  };

  return (
    <div className="space-y-8 max-w-7xl mx-auto">
      {/* Top Title & Header */}
      <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4 border-b border-white/[0.08] pb-6">
        <div>
          <div className="flex items-center gap-2 mb-1">
            <span className="text-xs font-semibold px-2.5 py-0.5 rounded-full bg-indigo-500/10 text-indigo-400 border border-indigo-500/20 flex items-center gap-1.5">
              <BookOpen className="w-3 h-3" />
              Developer Reference & OpenAPI v1
            </span>
          </div>
          <h1 className="text-2xl sm:text-3xl font-bold text-white tracking-tight">Interactive API Specification</h1>
          <p className="text-xs sm:text-sm text-slate-400">
            One unified REST API connecting Opn, GB Prime Pay, and bank rails with automatic failover.
          </p>
        </div>

        <div className="flex items-center gap-3">
          <button
            onClick={() => handleCopy("https://api.payflow.io/api/v1")}
            className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium text-slate-300 bg-slate-900/60 border border-white/[0.08] rounded-xl hover:bg-slate-800 hover:text-white transition-all"
          >
            {copied ? <Check className="w-3.5 h-3.5 text-emerald-400" /> : <Copy className="w-3.5 h-3.5" />}
            <span>Copy Base URL</span>
          </button>
          <a
            href="http://localhost:5000/openapi/v1.json"
            target="_blank"
            rel="noopener noreferrer"
            className="flex items-center gap-2 px-3.5 py-1.5 text-xs font-semibold text-white bg-indigo-600 rounded-xl hover:bg-indigo-500 shadow-md shadow-indigo-600/30 transition-all"
          >
            <span>Raw OpenAPI JSON</span>
            <ExternalLink className="w-3.5 h-3.5" />
          </a>
        </div>
      </div>

      {/* Main 2-Column Mintlify/Stripe style layout */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-8 items-start">
        {/* Left Column: Endpoints Navigation (3 cols) */}
        <div className="lg:col-span-3 space-y-4">
          <div className="text-[11px] font-semibold tracking-wider uppercase text-slate-400 px-1">
            API Endpoints
          </div>
          <div className="space-y-1">
            {endpoints.map((ep) => {
              const isSelected = ep.id === selectedEndpoint.id;
              return (
                <button
                  key={ep.id}
                  onClick={() => {
                    setSelectedEndpoint(ep);
                    setSelectedResponseIdx(0);
                  }}
                  className={`w-full flex items-center justify-between p-3 rounded-xl text-left text-xs transition-all border ${
                    isSelected
                      ? "bg-indigo-600/15 border-indigo-500/40 text-white shadow-md shadow-indigo-500/10"
                      : "bg-slate-900/40 border-white/[0.06] text-slate-400 hover:text-slate-200 hover:bg-slate-900/80"
                  }`}
                >
                  <div className="space-y-1 min-w-0 pr-2">
                    <div className="flex items-center gap-2">
                      <span
                        className={`text-[10px] font-mono font-bold px-1.5 py-0.5 rounded ${
                          ep.method === "POST"
                            ? "bg-indigo-500/20 text-indigo-300 border border-indigo-500/30"
                            : "bg-sky-500/20 text-sky-300 border border-sky-500/30"
                        }`}
                      >
                        {ep.method}
                      </span>
                      <span className="font-semibold truncate text-slate-200">{ep.name}</span>
                    </div>
                    <div className="text-[10px] font-mono text-slate-500 truncate">{ep.path}</div>
                  </div>
                  <ChevronRight className={`w-3.5 h-3.5 flex-shrink-0 ${isSelected ? "text-indigo-400" : "text-slate-600"}`} />
                </button>
              );
            })}
          </div>

          <div className="p-4 bg-slate-900/40 border border-white/[0.06] rounded-2xl space-y-2">
            <div className="flex items-center gap-2 text-xs font-semibold text-white">
              <Key className="w-3.5 h-3.5 text-indigo-400" />
              <span>Authentication</span>
            </div>
            <p className="text-[11px] text-slate-400 leading-relaxed">
              Every request requires <code className="text-indigo-300 font-mono">X-Merchant-Id</code> and <code className="text-indigo-300 font-mono">Idempotency-Key</code> in headers.
            </p>
          </div>
        </div>

        {/* Center / Right Columns: Documentation & Code Console (9 cols) */}
        <div className="lg:col-span-9 space-y-6">
          {/* Endpoint Header Card */}
          <div className="p-6 bg-slate-900/50 border border-white/[0.08] rounded-2xl backdrop-blur-xl shadow-xl space-y-4">
            <div className="flex flex-wrap items-center justify-between gap-3 border-b border-white/[0.06] pb-4">
              <div className="flex items-center gap-3">
                <span
                  className={`text-xs font-mono font-bold px-2.5 py-1 rounded-lg ${
                    selectedEndpoint.method === "POST"
                      ? "bg-indigo-500/20 text-indigo-300 border border-indigo-500/30"
                      : "bg-sky-500/20 text-sky-300 border border-sky-500/30"
                  }`}
                >
                  {selectedEndpoint.method}
                </span>
                <span className="text-sm sm:text-base font-mono font-semibold text-white">
                  {selectedEndpoint.path}
                </span>
              </div>
              <span className="px-2.5 py-0.5 rounded-full text-[10px] font-bold bg-slate-800 text-slate-300 border border-white/[0.08]">
                {selectedEndpoint.category}
              </span>
            </div>

            <p className="text-xs sm:text-sm text-slate-300 leading-relaxed">
              {selectedEndpoint.description}
            </p>

            {/* Headers Table */}
            {selectedEndpoint.headers.length > 0 && (
              <div className="space-y-2 pt-2">
                <h3 className="text-xs font-semibold uppercase tracking-wider text-slate-400">Request Headers</h3>
                <div className="overflow-x-auto border border-white/[0.06] rounded-xl">
                  <table className="w-full text-left text-xs text-slate-300">
                    <thead className="bg-slate-950/60 text-[11px] uppercase tracking-wider text-slate-500 border-b border-white/[0.06]">
                      <tr>
                        <th className="py-2.5 px-3">Header</th>
                        <th className="py-2.5 px-3">Type</th>
                        <th className="py-2.5 px-3">Description</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-white/[0.04] bg-slate-950/30 font-mono">
                      {selectedEndpoint.headers.map((h, idx) => (
                        <tr key={idx}>
                          <td className="py-2.5 px-3 font-semibold text-indigo-300">{h.name}</td>
                          <td className="py-2.5 px-3 text-[11px] text-slate-400">{h.required ? "required" : "optional"}</td>
                          <td className="py-2.5 px-3 font-sans text-slate-300">{h.description}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            )}

            {/* Request Body Parameters */}
            {selectedEndpoint.params.length > 0 && (
              <div className="space-y-2 pt-2">
                <h3 className="text-xs font-semibold uppercase tracking-wider text-slate-400">Body Parameters</h3>
                <div className="overflow-x-auto border border-white/[0.06] rounded-xl">
                  <table className="w-full text-left text-xs text-slate-300">
                    <thead className="bg-slate-950/60 text-[11px] uppercase tracking-wider text-slate-500 border-b border-white/[0.06]">
                      <tr>
                        <th className="py-2.5 px-3">Field</th>
                        <th className="py-2.5 px-3">Type</th>
                        <th className="py-2.5 px-3">Description</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-white/[0.04] bg-slate-950/30 font-mono">
                      {selectedEndpoint.params.map((p, idx) => (
                        <tr key={idx}>
                          <td className="py-2.5 px-3 font-semibold text-white">{p.name}</td>
                          <td className="py-2.5 px-3 text-[11px] text-amber-300">{p.type}</td>
                          <td className="py-2.5 px-3 font-sans text-slate-300">{p.description}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            )}
          </div>

          {/* Interactive Code Console & Response Inspector (Stripe / Mintlify style) */}
          <div className="bg-slate-900/60 border border-white/[0.08] rounded-2xl overflow-hidden backdrop-blur-xl shadow-2xl">
            {/* Console Toolbar */}
            <div className="p-3 bg-slate-950/80 border-b border-white/[0.08] flex flex-wrap items-center justify-between gap-3">
              <div className="flex items-center gap-1 bg-slate-900 p-1 rounded-xl border border-white/[0.06]">
                <button
                  onClick={() => setSelectedLang("curl")}
                  className={`px-3 py-1 rounded-lg text-xs font-semibold transition-all ${
                    selectedLang === "curl" ? "bg-indigo-600 text-white shadow" : "text-slate-400 hover:text-white"
                  }`}
                >
                  cURL
                </button>
                <button
                  onClick={() => setSelectedLang("csharp")}
                  className={`px-3 py-1 rounded-lg text-xs font-semibold transition-all ${
                    selectedLang === "csharp" ? "bg-indigo-600 text-white shadow" : "text-slate-400 hover:text-white"
                  }`}
                >
                  C# (.NET 10)
                </button>
                <button
                  onClick={() => setSelectedLang("typescript")}
                  className={`px-3 py-1 rounded-lg text-xs font-semibold transition-all ${
                    selectedLang === "typescript" ? "bg-indigo-600 text-white shadow" : "text-slate-400 hover:text-white"
                  }`}
                >
                  TypeScript (Next.js)
                </button>
              </div>

              <div className="flex items-center gap-2">
                <button
                  onClick={() => handleCopy(getActiveCode())}
                  className="p-1.5 px-2.5 rounded-lg bg-slate-900 border border-white/[0.08] text-xs font-medium text-slate-300 hover:text-white hover:bg-slate-800 transition-colors flex items-center gap-1"
                >
                  {copied ? <Check className="w-3.5 h-3.5 text-emerald-400" /> : <Copy className="w-3.5 h-3.5" />}
                  <span>{copied ? "Copied" : "Copy"}</span>
                </button>
                <button
                  onClick={handleTestCall}
                  disabled={isCalling}
                  className="flex items-center gap-1.5 px-3 py-1.5 bg-gradient-to-r from-emerald-600 to-emerald-500 hover:from-emerald-500 hover:to-emerald-400 text-white rounded-lg text-xs font-bold shadow-md shadow-emerald-600/30 transition-all disabled:opacity-50"
                >
                  <Play className="w-3 h-3 fill-current" />
                  <span>{isCalling ? "Sending..." : "Test Call"}</span>
                </button>
              </div>
            </div>

            {/* Code Body */}
            <div className="p-4 bg-[#070A0F] overflow-x-auto border-b border-white/[0.06]">
              <pre className="text-xs font-mono text-slate-300 leading-relaxed">
                <code>{getActiveCode()}</code>
              </pre>
            </div>

            {/* Live Response Payload Viewer */}
            <div className="p-4 bg-slate-950/90 space-y-3">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <span className="text-xs font-semibold text-slate-400 uppercase tracking-wider">Response Payload</span>
                  {liveCallSuccess && (
                    <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-emerald-500/20 text-emerald-300 border border-emerald-500/30 animate-in fade-in">
                      LIVE 200 OK RECEIVED
                    </span>
                  )}
                </div>

                {/* Response Variant Tabs */}
                <div className="flex items-center gap-1">
                  {selectedEndpoint.sampleResponses.map((r, idx) => (
                    <button
                      key={idx}
                      onClick={() => setSelectedResponseIdx(idx)}
                      className={`px-2.5 py-1 rounded-md text-[11px] font-mono transition-all ${
                        selectedResponseIdx === idx
                          ? "bg-slate-800 text-white font-bold border border-white/[0.1]"
                          : "text-slate-500 hover:text-slate-300"
                      }`}
                    >
                      {r.label}
                    </button>
                  ))}
                </div>
              </div>

              <div className="p-3 bg-[#070A0F] rounded-xl border border-white/[0.06] overflow-x-auto">
                <pre className="text-xs font-mono text-emerald-400 leading-relaxed">
                  <code>{JSON.stringify(selectedEndpoint.sampleResponses[selectedResponseIdx].payload, null, 2)}</code>
                </pre>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
