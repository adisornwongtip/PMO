"use client";

import { useState } from "react";
import { Copy, Check, Eye, EyeOff, Terminal, Key, ShieldCheck, Zap } from "lucide-react";

export default function DevelopersPage() {
  const [copied, setCopied] = useState(false);
  const [showSecret, setShowSecret] = useState(false);

  const apiKey = "pk_live_payflow_9481a8bf9241";
  const apiSecret = "sk_live_payflow_sec_9941088219481";

  const handleCopy = (text: string) => {
    navigator.clipboard.writeText(text);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
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

  return (
    <div className="space-y-6 max-w-7xl mx-auto">
      <div>
        <h1 className="text-2xl font-bold text-white tracking-tight">Developer Integration & API Keys</h1>
        <p className="text-sm text-slate-400">One API endpoint connects all payment gateways with automatic failover.</p>
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
                onClick={() => handleCopy(apiKey)}
                className="px-3 py-2 bg-slate-800 hover:bg-slate-700 text-slate-200 rounded-lg text-xs flex items-center gap-1.5 transition-colors"
              >
                {copied ? <Check className="w-3.5 h-3.5 text-emerald-400" /> : <Copy className="w-3.5 h-3.5" />}
                <span>Copy</span>
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
              >
                {showSecret ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
              </button>
              <button
                onClick={() => handleCopy(apiSecret)}
                className="px-3 py-2 bg-slate-800 hover:bg-slate-700 text-slate-200 rounded-lg text-xs flex items-center gap-1.5 transition-colors"
              >
                <Copy className="w-3.5 h-3.5" />
                <span>Copy</span>
              </button>
            </div>
          </div>
        </div>
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
