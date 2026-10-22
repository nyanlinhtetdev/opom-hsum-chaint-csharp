using System;
using System.Collections.Generic;

namespace HsumChaint.Database.Models;

public partial class SpaceRolePermission
{
    public int Id { get; set; }

    public int SpaceRoleId { get; set; }

    public int SpacePermissionId { get; set; }

    public bool IsDeleted { get; set; }

    public virtual SpaceRole SpaceRole { get; set; } = null!;

    public virtual SpacePermission SpacePermission { get; set; } = null!;
}
