using System;
using System.Collections.Generic;

namespace HsumChaint.Database.Models;

public partial class SpacePermission
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<SpaceRolePermission> SpaceRolePermissions { get; set; } = new List<SpaceRolePermission>();
}
