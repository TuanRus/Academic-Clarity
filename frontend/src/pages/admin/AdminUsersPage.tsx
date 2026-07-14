import { useEffect, useMemo, useState } from 'react';
import AdminBadge from '../../components/admin/AdminBadge';
import AdminMetricCard from '../../components/admin/AdminMetricCard';
import AdminModal from '../../components/admin/AdminModal';
import AdminSectionCard from '../../components/admin/AdminSectionCard';
import AdminTable from '../../components/admin/AdminTable';
import AdminToast from '../../components/admin/AdminToast';
import { getUsers, updateUserStatus, updateUserRole, roleIdFromName, type UserDirectoryRow } from '../../lib/api/admin';

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

  const filteredUsers = useMemo(() => users.filter((user) => `${user.id} ${user.name} ${user.email} ${user.role}`.toLowerCase().includes(query.toLowerCase())), [query, users]);
  const pageSize = 4;
  const totalPages = Math.max(1, Math.ceil(filteredUsers.length / pageSize));
  const visibleUsers = filteredUsers.slice((page - 1) * pageSize, page * pageSize);
  const activeUsers = users.filter((user) => user.status === 'ACTIVE').length;
  const pendingUsers = users.filter((user) => user.status === 'REGISTERED').length;
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

  const downloadComplianceReport = () => {
    const content = `Total Users,Active Users,Pending Role Requests\n${users.length},${activeUsers},${pendingUsers}`;
    const blob = new Blob([content], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = 'compliance-report.csv';
    anchor.click();
    URL.revokeObjectURL(url);
    setToast('Compliance report downloaded.');
  };

  return (
    <div className="space-y-5">
      <AdminToast message={toast} onClose={() => setToast(null)} />

      <div>
        <h1 className="text-2xl font-extrabold tracking-tight text-slate-950">User Directory Governance</h1>
        <p className="mt-1 text-xs text-slate-500">Manage institution access, audit role transitions, and oversee global RBAC alignment across academic departments.</p>
      </div>

      <div className="grid gap-5 lg:grid-cols-3">
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

        <AdminMetricCard
          label="Pending Role Requests"
          value={String(pendingUsers)}
          helper="Registered users awaiting review"
          icon="▣"
          accent="orange"
        />
      </div>

      <div className="space-y-5">
        <div className="space-y-5">
          <AdminSectionCard
            title="Access Control List"
            action={
              <div className="flex items-center gap-3">
                <input value={query} onChange={(event) => { setQuery(event.target.value); setPage(1); }} placeholder="Search user..." className="rounded-md border border-slate-200 px-3 py-2 text-xs outline-none focus:border-[#0b6fb8]" />
                <button onClick={() => setShowProvisionModal(true)} className="rounded-md bg-[#4338ca] hover:bg-[#3730a3] px-4 py-2 text-xs font-bold text-white">+ Provision User</button>
              </div>
            }
          >
            <AdminTable headers={['User ID', 'Entity Full Name', 'Registered Email Node', 'Role Authorization', 'Status', 'Actions']}>
              {visibleUsers.map((directoryUser) => (
                <tr key={directoryUser.id} className="hover:bg-slate-50">
                  <td className="px-5 py-4 font-bold text-slate-700">{directoryUser.id}</td>
                  <td className="px-5 py-4">
                    <div className="flex items-center gap-3">
                      <span className="flex h-8 w-8 items-center justify-center rounded-full bg-slate-100 text-[10px] font-bold text-slate-600">{directoryUser.initials}</span>
                      <span className="font-bold text-slate-900">{directoryUser.name}</span>
                    </div>
                  </td>
                  <td className="px-5 py-4 text-slate-500">{directoryUser.email}</td>
                  <td className="px-5 py-4">
                    <select value={directoryUser.role} onChange={(event) => changeRole(directoryUser.id, event.target.value)} className="rounded border border-slate-200 bg-white px-2 py-1 text-xs">
                      {roleOptions.map((role) => <option key={role}>{role}</option>)}
                    </select>
                  </td>
                  <td className="px-5 py-4"><AdminBadge status={directoryUser.status} /></td>
                  <td className="px-5 py-4">
                    <div className="flex flex-wrap gap-2 text-xs font-bold">
                      <button onClick={() => setSelectedUser(directoryUser)} className="text-[#0b6fb8] hover:underline">View</button>
                      <button onClick={() => toggleUserStatus(directoryUser)} className="text-orange-700 hover:underline">{directoryUser.status === 'SUSPENDED' ? 'Activate' : 'Suspend'}</button>
                      {directoryUser.status === 'REGISTERED' && <button onClick={() => approveResearcher(directoryUser)} className="text-emerald-700 hover:underline">Approve</button>}
                    </div>
                  </td>
                </tr>
              ))}
            </AdminTable>
            <div className="flex items-center justify-between border-t border-slate-100 px-5 py-3 text-xs text-slate-500">
              <span>Displaying {visibleUsers.length} of {filteredUsers.length} user entries</span>
              <div className="flex gap-1">
                {Array.from({ length: totalPages }, (_, index) => index + 1).map((pageNumber) => (
                  <button key={pageNumber} onClick={() => setPage(pageNumber)} className={`h-7 w-7 rounded border text-xs font-bold ${pageNumber === page ? 'bg-[#062b4f] text-white' : 'border-slate-200 bg-white text-slate-600'}`}>{pageNumber}</button>
                ))}
              </div>
            </div>
          </AdminSectionCard>

          <AdminSectionCard title="Compliance Status">
            <div className="p-5">
              <p className="text-sm text-slate-600">The current RBAC configuration matches the institutional policy 2024-B. No unauthorized role elevations detected in the last 24 hours.</p>
              <button onClick={downloadComplianceReport} className="mt-3 text-xs font-bold text-[#0b6fb8]">Download Compliance Report →</button>
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
            <button onClick={() => setShowProvisionModal(false)} className="rounded-md border border-slate-300 bg-white px-4 py-2 text-xs font-bold text-slate-700">Cancel</button>
            <button onClick={provisionUser} className="rounded-md bg-[#4338ca]b hover:bg-[#3730a3] px-4 py-2 text-xs font-bold text-white">Create User</button>
          </>
        }
      >
        <div className="space-y-4">
          <input value={newUserName} onChange={(event) => setNewUserName(event.target.value)} placeholder="Full name" className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[#0b6fb8]" />
          <input value={newUserEmail} onChange={(event) => setNewUserEmail(event.target.value)} placeholder="Email" className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[#0b6fb8]" />
          <select value={newUserRole} onChange={(event) => setNewUserRole(event.target.value)} className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[#0b6fb8]">
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
          <div className="space-y-3 text-sm text-slate-700">
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
