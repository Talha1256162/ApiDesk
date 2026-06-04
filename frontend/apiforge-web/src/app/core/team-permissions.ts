export type TeamRole = 'Owner' | 'Admin' | 'Editor' | 'Viewer' | string;

export function canManageMember(actorRole: TeamRole, targetRole: TeamRole, action: 'changeRole' | 'remove' | 'invite'): boolean {
  if (actorRole === 'Owner') {
    return true;
  }

  if (actorRole === 'Admin') {
    return targetRole !== 'Owner';
  }

  return action === 'invite' ? false : false;
}

export function canEditWorkspaceContent(role: TeamRole): boolean {
  return role === 'Owner' || role === 'Admin' || role === 'Editor';
}
