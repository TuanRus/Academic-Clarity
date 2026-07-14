import { useState } from 'react';
import AdminSectionCard from '../../components/admin/AdminSectionCard';
import AdminToast from '../../components/admin/AdminToast';
import { sendBroadcast } from '../../lib/api/admin';
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

    return (
        <div className="space-y-5">
            <AdminToast message={toast} onClose={() => setToast(null)} />

            <div>
                <h1 className="text-2xl font-extrabold tracking-tight text-slate-950">
                    System Settings
                </h1>
                <p className="mt-1 text-xs text-slate-500">
                    Configure system information, OpenAlex integration, notifications and security.
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

            <div className="grid gap-5 xl:grid-cols-2">
                <AdminSectionCard title="General Settings" subtitle="Basic system information.">
                    <div className="space-y-4 p-5">
                        <input className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm" defaultValue="AIS Journal Trend System" />
                        <input className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm" defaultValue="FPT University" />
                        <select className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm" defaultValue="GMT+7">
                            <option>GMT+7</option>
                            <option>UTC</option>
                        </select>
                    </div>
                </AdminSectionCard>

                <AdminSectionCard title="OpenAlex Integration" subtitle="External academic data source configuration.">
                    <div className="space-y-4 p-5">
                        <input className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm" defaultValue="********************" />
                        <select className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm" defaultValue="Daily">
                            <option>Daily</option>
                            <option>Weekly</option>
                            <option>Manual</option>
                        </select>
                        <button
                            onClick={() => setToast('OpenAlex settings saved.')}
                            className="rounded-md bg-[#4338ca] hover:bg-[#3730a3] px-4 py-2 text-xs font-bold text-white"
                        >
                            Save OpenAlex Config
                        </button>
                    </div>
                </AdminSectionCard>

                <AdminSectionCard title="Notification Settings" subtitle="Choose events that notify admins.">
                    <div className="space-y-4 p-5">
                        {['Notify when sync failed', 'Notify when new user registered', 'Notify when payment success'].map((item) => (
                            <label key={item} className="flex items-center justify-between rounded-md bg-slate-50 px-4 py-3 text-sm font-semibold">
                                <span>{item}</span>
                                <input type="checkbox" defaultChecked className="h-4 w-4" />
                            </label>
                        ))}
                    </div>
                </AdminSectionCard>

                <AdminSectionCard title="Security Settings" subtitle="Control admin session and authentication behavior.">
                    <div className="space-y-4 p-5">
                        <select className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm" defaultValue="60 minutes">
                            <option>30 minutes</option>
                            <option>60 minutes</option>
                            <option>120 minutes</option>
                        </select>

                        <label className="flex items-center justify-between rounded-md bg-slate-50 px-4 py-3 text-sm font-semibold">
                            <span>Require 2FA for admin</span>
                            <input type="checkbox" className="h-4 w-4" />
                        </label>

                        <button
                            onClick={() => setToast('Security settings saved.')}
                            className="rounded-md bg-[#4338ca] hover:bg-[#3730a3] px-4 py-2 text-xs font-bold text-white"
                        >
                            Save Security Config
                        </button>
                    </div>
                </AdminSectionCard>
            </div>
        </div>
    );
};

export default AdminSettingsPage;