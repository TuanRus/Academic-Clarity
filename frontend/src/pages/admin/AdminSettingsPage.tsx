import { useEffect, useState, type ReactNode } from 'react';
import AdminSectionCard from '../../components/admin/AdminSectionCard';
import AdminToast from '../../components/admin/AdminToast';
import { sendBroadcast, getSystemConfig, type SystemConfig } from '../../lib/api/admin';
import { ApiError } from '../../lib/http';

const AdminSettingsPage = () => {
    const [toast, setToast] = useState<string | null>(null);

    // System broadcast — gửi thông báo tới TOÀN hệ thống.
    const [bcTitle, setBcTitle] = useState('');
    const [bcMessage, setBcMessage] = useState('');
    const [sending, setSending] = useState(false);
    const handleBroadcast = async () => {
        if (!bcTitle.trim() || !bcMessage.trim()) { setToast('Title and message are required.'); return; }
        setSending(true);
        try {
            await sendBroadcast(bcTitle.trim(), bcMessage.trim());
            setToast('Broadcast sent to all users.');
            setBcTitle(''); setBcMessage('');
        } catch (e) {
            setToast(e instanceof ApiError ? e.message : 'Failed to send broadcast.');
        } finally {
            setSending(false);
        }
    };

    // Cấu hình hệ thống (read-only, không bí mật).
    const [config, setConfig] = useState<SystemConfig | null>(null);
    const [configError, setConfigError] = useState<string | null>(null);
    useEffect(() => {
        getSystemConfig()
            .then(setConfig)
            .catch((e) => setConfigError(e instanceof ApiError ? e.message : 'Could not load system configuration.'));
    }, []);

    // Hàng key–value read-only.
    const Row = ({ label, value }: { label: string; value: ReactNode }) => (
        <div className="flex items-start justify-between gap-4 border-b border-slate-100 py-2 last:border-0">
            <span className="text-sm text-slate-500">{label}</span>
            <span className="text-right text-sm font-semibold text-slate-800 break-all">{value}</span>
        </div>
    );

    return (
        <div className="space-y-5">
            <AdminToast message={toast} onClose={() => setToast(null)} />

            <div>
                <h1 className="text-2xl font-extrabold tracking-tight text-slate-950">
                    System Settings
                </h1>
                <p className="mt-1 text-xs text-slate-500">
                    Send system-wide announcements and review the current (read-only) system configuration.
                </p>
            </div>

            <AdminSectionCard title="System Broadcast" subtitle="Send an announcement (alert, maintenance, notice…) to every user.">
                <div className="space-y-3 p-5">
                    <input
                        value={bcTitle}
                        onChange={(e) => setBcTitle(e.target.value)}
                        placeholder="Broadcast title (e.g. Scheduled maintenance)"
                        className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[#4338ca]"
                    />
                    <textarea
                        value={bcMessage}
                        onChange={(e) => setBcMessage(e.target.value)}
                        rows={3}
                        placeholder="Message sent to all users…"
                        className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[#4338ca]"
                    />
                    <button
                        onClick={handleBroadcast}
                        disabled={sending}
                        className="rounded-md bg-[#4338ca] hover:bg-[#3730a3] px-4 py-2 text-xs font-bold text-white disabled:opacity-50"
                    >
                        {sending ? 'Sending…' : 'Send to all users'}
                    </button>
                </div>
            </AdminSectionCard>

            <AdminSectionCard
                title="System Configuration"
                subtitle="Current operational configuration (read-only). Secrets are never shown here."
                action={
                    <span className="shrink-0 rounded-full bg-slate-100 px-2.5 py-1 text-[10px] font-bold uppercase tracking-wide text-slate-500">
                        Read-only
                    </span>
                }
            >
                <div className="p-5">
                    {configError && <p className="text-sm text-red-600">{configError}</p>}
                    {!config && !configError && <p className="text-sm text-slate-400">Loading…</p>}

                    {config && (
                        <div className="grid gap-x-10 gap-y-1 xl:grid-cols-2">
                            <div>
                                <h3 className="mb-1 text-xs font-bold uppercase tracking-wide text-slate-400">Runtime</h3>
                                <Row label="Environment" value={config.environment} />
                                <Row label=".NET version" value={config.dotnetVersion} />
                                <Row label="Allowed hosts" value={config.allowedHosts || '—'} />
                                <Row label="Default log level" value={config.defaultLogLevel || '—'} />
                                <Row
                                    label="Auto weekly sync"
                                    value={
                                        <span className={config.weeklySyncEnabled ? 'text-emerald-600' : 'text-slate-400'}>
                                            {config.weeklySyncEnabled ? 'Enabled' : 'Disabled'}
                                        </span>
                                    }
                                />
                            </div>

                            <div>
                                <h3 className="mb-1 text-xs font-bold uppercase tracking-wide text-slate-400">OpenAlex &amp; Auth</h3>
                                <Row label="OpenAlex base URL" value={config.openAlexBaseUrl || '—'} />
                                <Row label="OpenAlex contact email" value={config.openAlexEmail || '—'} />
                                <Row label="JWT issuer" value={config.jwtIssuer || '—'} />
                                <Row label="JWT audience" value={config.jwtAudience || '—'} />
                            </div>

                            <div>
                                <h3 className="mb-1 mt-4 text-xs font-bold uppercase tracking-wide text-slate-400">AI providers</h3>
                                {config.aiProviders.length === 0 && <p className="py-2 text-sm text-slate-400">None configured.</p>}
                                {config.aiProviders.map((p) => (
                                    <Row key={p.name} label={p.name} value={`${p.model} · ${p.baseUrl}`} />
                                ))}
                                <Row label="Gemini model" value={config.geminiModel || '—'} />
                                <Row label="Gemini base URL" value={config.geminiBaseUrl || '—'} />
                                <Row label="Gemini timeout (s)" value={config.geminiTimeoutSeconds} />
                            </div>

                            <div>
                                <h3 className="mb-1 mt-4 text-xs font-bold uppercase tracking-wide text-slate-400">Integrations</h3>
                                {config.integrations.map((it) => (
                                    <Row
                                        key={it.name}
                                        label={it.name}
                                        value={
                                            <span className={it.configured ? 'text-emerald-600' : 'text-slate-400'}>
                                                {it.configured ? '✓ Configured' : '✗ Not configured'}
                                            </span>
                                        }
                                    />
                                ))}
                            </div>
                        </div>
                    )}
                </div>
            </AdminSectionCard>
        </div>
    );
};

export default AdminSettingsPage;
