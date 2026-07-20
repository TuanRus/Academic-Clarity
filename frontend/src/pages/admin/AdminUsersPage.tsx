import { useEffect, useMemo, useState } from 'react';
import AdminBadge from '../../components/admin/AdminBadge';
import AdminMetricCard from '../../components/admin/AdminMetricCard';
import AdminModal from '../../components/admin/AdminModal';
import AdminSectionCard from '../../components/admin/AdminSectionCard';
import AdminTable from '../../components/admin/AdminTable';
import AdminToast from '../../components/admin/AdminToast';
import { getUsers, updateUserStatus, updateUserRole, roleIdFromName, sendBroadcast, type UserDirectoryRow } from '../../lib/api/admin';
import { ApiError } from '../../lib/http';

// Phải khớp CHÍNH XÁC với nhãn role do roleName() trả về (admin.ts), nếu không <select>
// không tìm được option khớp → mọi dòng đều rơi về option đầu tiên.
const roleOptions = ['ADMIN', 'RESEARCHER', 'STUDENT', 'Member'];

const AdminUsersPage = () => {
  const [users, setUsers] = useState<UserDirectoryRow[]>([]);
  useEffect(() => { getUsers().then(setUsers).catch(() => setUsers([])); }, []);
  const [query, setQuery] = useState('');
  const [page, setPage] = useState(1);
  const [selectedUser, setSelectedUser] = useState<UserDirectoryRow | null>(null);
  const [showProvisionModal, setShowProvisionModal] = useState(false);
  const [newUserName, setNewUserName] = useState('');
  const [newUserEmail, setNewUserEmail] = useState('');
  const [newUserRole, setNewUserRole] = useState(roleOptions[3]);
  const [toast, setToast] = useState<string | null>(null);

  // System broadcast — gửi thông báo tới TOÀN bộ người dùng (chuyển từ Settings sang đây).
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

  const filteredUsers = useMemo(() => users.filter((user) => `${user.id} ${user.name} ${user.email} ${user.role}`.toLowerCase().includes(query.toLowerCase())), [query, users]);
  const pageSize = 4;
  const totalPages = Math.max(1, Math.ceil(filteredUsers.length / pageSize));
  const visibleUsers = filteredUsers.slice((page - 1) * pageSize, page * pageSize);
  const activeUsers = users.filter((user) => user.status === 'ACTIVE').length;
  const totalUsers = users.length;


  const changeRole = (userId: string, role: string) => {
    // Lưu DB thật rồi cập nhật UI (rollback bằng reload nếu lỗi).
    updateUserRole(userId, roleIdFromName(role))
      .then(() => {
        setUsers((current) => current.map((user) => (user.id === userId ? { ...user, role } : user)));
        setToast(`${userId} role updated.`);
      })
      .catch(() => setToast('Update role failed.'));
  };

  const toggleUserStatus = (user: UserDirectoryRow) => {
    const nextStatus = user.status === 'SUSPENDED' ? 'ACTIVE' : 'SUSPENDED';
    // Lưu DB thật rồi cập nhật UI (rollback bằng reload nếu lỗi).
    updateUserStatus(user.id, nextStatus === 'ACTIVE')
      .then(() => {
        setUsers((current) => current.map((item) => (item.id === user.id ? { ...item, status: nextStatus } : item)));
        setToast(`${user.name} is now ${nextStatus}.`);
      })
      .catch(() => setToast('Update status failed.'));
  };

  const approveResearcher = (user: UserDirectoryRow) => {
    setUsers((current) => current.map((item) => (item.id === user.id ? { ...item, role: 'RESEARCHER', status: 'ACTIVE' } : item)));
    setToast(`${user.name} approved as Researcher.`);
  };

  const denyResearcher = (user: UserDirectoryRow) => {
    setUsers((current) => current.map((item) => (item.id === user.id ? { ...item, status: 'SUSPENDED' } : item)));
    setToast(`${user.name} request denied.`);
  };

  const provisionUser = () => {
    if (!newUserName.trim() || !newUserEmail.includes('@')) {
      setToast('Please enter a valid full name and email.');
      return;
    }

    const initials = newUserName
      .split(' ')
      .filter(Boolean)
      .map((part) => part[0])
      .slice(0, 2)
      .join('')
      .toUpperCase();

    const nextUser: UserDirectoryRow = {
      id: `#USR-${Math.floor(Math.random() * 9000 + 1000)}`,
      initials,
      name: newUserName.trim(),
      email: newUserEmail.trim(),
      role: newUserRole,
      status: 'REGISTERED',
    };

    setUsers((current) => [nextUser, ...current]);
    setNewUserName('');
    setNewUserEmail('');
    setNewUserRole(roleOptions[3]);
    setShowProvisionModal(false);
    setToast(`${nextUser.name} has been provisioned.`);
  };

  return (
    <div className="space-y-5">
      <AdminToast message={toast} onClose={() => setToast(null)} />

      <div>
        <h1 className="text-2xl font-bold tracking-tight text-gray-900">User Account Management</h1>
        <p className="mt-1 text-xs text-gray-500">Manage user roles, account status, and system access.</p>
      </div>

      <div className="grid gap-5 lg:grid-cols-2">
        <AdminMetricCard
          label="Total Users"
          value={String(totalUsers)}
          helper="All registered user accounts"
          icon="👥"
          accent="blue"
        />

        <AdminMetricCard
          label="Active Users"
          value={String(activeUsers)}
          helper="Accounts currently active"
          icon="✓"
          accent="green"
        />
      </div>

      <AdminSectionCard title="System Broadcast" subtitle="Send an announcement (alert, maintenance, notice…) to every user.">
        <div className="space-y-3 p-5">
          <input
            value={bcTitle}
            onChange={(e) => setBcTitle(e.target.value)}
            placeholder="Broadcast title (e.g. Scheduled maintenance)"
            className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm outline-none focus:border-indigo-700"
          />
          <textarea
            value={bcMessage}
            onChange={(e) => setBcMessage(e.target.value)}
            rows={3}
            placeholder="Message sent to all users…"
            className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm outline-none focus:border-indigo-700"
          />
          <button
            onClick={handleBroadcast}
            disabled={sending}
            className="rounded-md bg-indigo-700 hover:bg-indigo-800 px-4 py-2 text-xs font-bold text-white disabled:opacity-50"
          >
            {sending ? 'Sending…' : 'Send to all users'}
          </button>
        </div>
      </AdminSectionCard>

      <div className="space-y-5">
        <div className="space-y-5">
          <AdminSectionCard
            title="Access Control List"
            action={
              <div className="flex items-center gap-3">
                <input value={query} onChange={(event) => { setQuery(event.target.value); setPage(1); }} placeholder="Search user..." className="rounded-md border border-gray-200 px-3 py-2 text-xs outline-none focus:border-indigo-700" />
                <button onClick={() => setShowProvisionModal(true)} className="rounded-md bg-indigo-700 hover:bg-indigo-800 px-4 py-2 text-xs font-bold text-white">+ Provision User</button>
              </div>
            }
          >
            <AdminTable headers={['User ID', 'Entity Full Name', 'Registered Email Node', 'Role Authorization', 'Status', 'Actions']}>
              {visibleUsers.map((directoryUser) => (
                <tr key={directoryUser.id} className="hover:bg-gray-50">
                  <td className="px-5 py-4 font-bold text-gray-700">{directoryUser.id}</td>
                  <td className="px-5 py-4">
                    <div className="flex items-center gap-3">
                      <span className="flex h-8 w-8 items-center justify-center rounded-full bg-gray-100 text-[10px] font-bold text-gray-600">{directoryUser.initials}</span>
                      <span className="font-bold text-gray-900">{directoryUser.name}</span>
                    </div>
                  </td>
                  <td className="px-5 py-4 text-gray-500">{directoryUser.email}</td>
                  <td className="px-5 py-4">
                    <select value={directoryUser.role} onChange={(event) => changeRole(directoryUser.id, event.target.value)} className="rounded border border-gray-200 bg-white px-2 py-1 text-xs">
                      {roleOptions.map((role) => <option key={role}>{role}</option>)}
                    </select>
                  </td>
                  <td className="px-5 py-4"><AdminBadge status={directoryUser.status} /></td>
                  <td className="px-5 py-4">
                    <div className="flex flex-wrap gap-2 text-xs font-bold">
                      <button onClick={() => setSelectedUser(directoryUser)} className="text-indigo-700 hover:underline">View</button>
                      <button onClick={() => toggleUserStatus(directoryUser)} className="text-orange-700 hover:underline">{directoryUser.status === 'SUSPENDED' ? 'Activate' : 'Suspend'}</button>
                      {directoryUser.status === 'REGISTERED' && <button onClick={() => approveResearcher(directoryUser)} className="text-emerald-700 hover:underline">Approve</button>}
                    </div>
                  </td>
                </tr>
              ))}
            </AdminTable>
            <div className="flex items-center justify-between border-t border-gray-100 px-5 py-3 text-xs text-gray-500">
              <span>Displaying {visibleUsers.length} of {filteredUsers.length} user entries</span>
              <div className="flex gap-1">
                {Array.from({ length: totalPages }, (_, index) => index + 1).map((pageNumber) => (
                  <button key={pageNumber} onClick={() => setPage(pageNumber)} className={`h-7 w-7 rounded border text-xs font-bold ${pageNumber === page ? 'bg-indigo-800 text-white' : 'border-gray-200 bg-white text-gray-600'}`}>{pageNumber}</button>
                ))}
              </div>
            </div>
          </AdminSectionCard>
        </div>
      </div>

      <AdminModal
        open={showProvisionModal}
        title="Provision User"
        subtitle="Create a pending user account for admin review."
        onClose={() => setShowProvisionModal(false)}
        footer={
          <>
            <button onClick={() => setShowProvisionModal(false)} className="rounded-md border border-gray-300 bg-white px-4 py-2 text-xs font-bold text-gray-700">Cancel</button>
            <button onClick={provisionUser} className="rounded-md bg-indigo-700b hover:bg-indigo-800 px-4 py-2 text-xs font-bold text-white">Create User</button>
          </>
        }
      >
        <div className="space-y-4">
          <input value={newUserName} onChange={(event) => setNewUserName(event.target.value)} placeholder="Full name" className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm outline-none focus:border-indigo-700" />
          <input value={newUserEmail} onChange={(event) => setNewUserEmail(event.target.value)} placeholder="Email" className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm outline-none focus:border-indigo-700" />
          <select value={newUserRole} onChange={(event) => setNewUserRole(event.target.value)} className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm outline-none focus:border-indigo-700">
            {roleOptions.map((role) => <option key={role}>{role}</option>)}
          </select>
        </div>
      </AdminModal>

      <AdminModal
        open={Boolean(selectedUser)}
        title="User Detail"
        subtitle="Role, status and access information."
        onClose={() => setSelectedUser(null)}
        footer={selectedUser ? <button onClick={() => denyResearcher(selectedUser)} className="rounded-md border border-red-200 bg-red-50 px-4 py-2 text-xs font-bold text-red-700">Deny Request</button> : undefined}
      >
        {selectedUser && (
          <div className="space-y-3 text-sm text-gray-700">
            <p><span className="font-bold">User ID:</span> {selectedUser.id}</p>
            <p><span className="font-bold">Name:</span> {selectedUser.name}</p>
            <p><span className="font-bold">Email:</span> {selectedUser.email}</p>
            <p><span className="font-bold">Role:</span> {selectedUser.role}</p>
            <p><span className="font-bold">Status:</span> <AdminBadge status={selectedUser.status} /></p>
          </div>
        )}
      </AdminModal>
    </div>
  );
};

export default AdminUsersPage;
