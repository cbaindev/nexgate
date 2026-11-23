// Generado por `npm run api:generate` desde api/nexgate-v1.openapi.json. No editar a mano.
/* eslint-disable */
export interface paths {
    "/api/v1/access-points/{id}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /**
         * Obtener un punto de acceso
         * @description Un punto fuera del alcance responde igual que uno inexistente (404).
         */
        get: operations["getAccessPoint"];
        /**
         * Modificar un punto de acceso
         * @description Cambia todos sus atributos salvo el sitio.
         */
        put: operations["updateAccessPoint"];
        post?: never;
        /** Eliminar un punto de acceso */
        delete: operations["deleteAccessPoint"];
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/access-points/{id}/policy": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /**
         * Obtener la política compilada de un punto de acceso
         * @description Formato v1: zona horaria, desplazamientos UTC, ventanas, credenciales (HMAC con la clave del sitio), entradas y hash del contenido.
         */
        get: operations["getAccessPointPolicy"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/access-points/{id}/simulations": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        /**
         * Simular una pasada por un punto de acceso
         * @description Compila la política vigente y decide con la credencial presentada en el instante indicado (ahora, si no se indica). Motivos: allowed, credential_invalid, explicit_deny o no_permission.
         */
        post: operations["simulateAccess"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/auth/logout": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        /**
         * Cerrar sesión
         * @description Revoca la sesión del token de renovación (del cuerpo o, sin él, de la cookie nexgate_refresh) y borra la cookie. Responde igual aunque el token no exista o ya esté revocado.
         */
        post: operations["signOut"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/auth/refresh": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        /**
         * Renovar los tokens o cambiar de organización activa
         * @description Rota el token de renovación: el usado deja de valer y reutilizarlo revoca la sesión entera. Con organizationId cambia la organización activa. Sin refreshToken en el cuerpo usa la cookie nexgate_refresh, la reemplaza por la nueva y responde refreshToken nulo.
         */
        post: operations["refreshToken"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/auth/token": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        /**
         * Iniciar sesión
         * @description Valida correo y contraseña y emite el token de acceso y el de renovación. Con una sola membresía la organización queda elegida; con varias, se elige con organizationId. Con refreshTokenInCookie el token de renovación viaja solo en la cookie httpOnly nexgate_refresh y refreshToken es nulo. Credenciales inválidas, cuenta inexistente y cuenta deshabilitada responden igual (401 invalid_credentials).
         */
        post: operations["signIn"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/credentials/{id}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /** Obtener una credencial */
        get: operations["getCredential"];
        /** Modificar la vigencia de una credencial */
        put: operations["updateCredential"];
        post?: never;
        /** Eliminar una credencial */
        delete: operations["deleteCredential"];
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/device/claim": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        /**
         * Reclamar un dispositivo
         * @description Con el código de reclamo y una solicitud de certificado PKCS#10 (PEM, clave P-256). Devuelve el certificado de cliente, el de la autoridad, la clave HMAC del sitio (base64) y la clave pública que verifica las políticas. Un código inexistente, usado o vencido responde 400 invalid_claim.
         */
        post: operations["claimDevice"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/device/events": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        /**
         * Enviar eventos de acceso
         * @description Lote de 1 a 100 eventos con el identificador que generó el dispositivo (un reenvío no duplica), el instante en que ocurrieron (también los encolados sin conexión), el punto, el hash de la credencial si la leyó, la decisión, el motivo y la versión de política. Se valida el lote entero antes de guardar.
         */
        post: operations["sendDeviceEvents"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/device/heartbeat": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        /**
         * Latido del dispositivo
         * @description Registra el último contacto y las versiones de firmware y de política. Responde la versión de política vigente, la hora del servidor (UTC) y cada cuántos segundos volver a latir.
         */
        post: operations["deviceHeartbeat"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/device/policy": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /**
         * Descargar la política del dispositivo
         * @description Paquete con la política v1 de cada punto que controla el dispositivo y una versión que solo avanza cuando cambia el contenido (encabezado ETag). Con If-None-Match y la versión vigente responde 304. La firma ECDSA P-256 (IEEE P1363, base64url) de los bytes exactos del cuerpo viaja en X-Nexgate-Signature.
         */
        get: operations["getDevicePolicy"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/devices/{id}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /** Obtener un dispositivo */
        get: operations["getDevice"];
        /** Cambiar el nombre de un dispositivo */
        put: operations["updateDevice"];
        post?: never;
        /**
         * Eliminar un dispositivo
         * @description Su certificado deja de valer y sus puntos quedan libres. Sus eventos se conservan.
         */
        delete: operations["deleteDevice"];
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/devices/{id}/access-points": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        /**
         * Asignar los puntos de acceso que controla un dispositivo
         * @description Reemplaza el conjunto: puntos del sitio del dispositivo que no controle otro dispositivo (409 access_point_assigned).
         */
        put: operations["assignDeviceAccessPoints"];
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/devices/{id}/claim-code": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        /**
         * Emitir un nuevo código de reclamo
         * @description El dispositivo vuelve a pendiente y su certificado anterior deja de valer. El código solo aparece en esta respuesta.
         */
        post: operations["issueDeviceClaimCode"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/devices/{id}/revocation": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        /**
         * Revocar un dispositivo
         * @description Su certificado deja de valer en la solicitud siguiente. Conserva sus puntos y se puede volver a reclamar con un código nuevo.
         */
        post: operations["revokeDevice"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/grants/{id}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /** Obtener un otorgamiento */
        get: operations["getGrant"];
        put?: never;
        post?: never;
        /**
         * Eliminar un otorgamiento
         * @description Los otorgamientos no se modifican: se eliminan y se crea otro.
         */
        delete: operations["deleteGrant"];
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/invitations": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        /**
         * Invitar a una persona a la organización activa
         * @description Crea una invitación con rol y alcance. La respuesta es la misma tenga o no cuenta la persona invitada. Mientras no haya envío de correos, el token de la invitación se devuelve a quien invita.
         */
        post: operations["createInvitation"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/invitations/accept": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        /**
         * Aceptar una invitación
         * @description Sin cuenta, la crea con la contraseña indicada. Con cuenta existente, exige el token de acceso de esa cuenta (401 si no viene). Después hay que iniciar sesión para obtener tokens con la organización.
         */
        post: operations["acceptInvitation"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/me": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /**
         * Usuario actual y sus organizaciones
         * @description Devuelve la cuenta, la organización activa y las organizaciones donde el usuario es miembro, para el selector.
         */
        get: operations["getCurrentUser"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/me/permissions": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /**
         * Permisos efectivos en la organización activa
         * @description Permisos administrativos del usuario según sus roles vigentes, con alcance de organización o la lista de sitios enteros y la de espacios (cada uno cubre su subárbol). La interfaz muestra solo lo permitido; el servidor igual los verifica en cada operación.
         */
        get: operations["getMyPermissions"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/members": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /** Listar los miembros de la organización activa */
        get: operations["listMembers"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/members/{userId}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post?: never;
        /**
         * Dar de baja a un miembro
         * @description Termina la membresía y sus roles con efecto en la solicitud siguiente. No se puede dar de baja al último administrador (409 last_administrator).
         */
        delete: operations["removeMember"];
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/members/{userId}/role-assignments": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        /**
         * Asignar un rol a un miembro
         * @description Rol con alcance de organización, de un sitio o de un espacio (con su subárbol), con vigencia opcional.
         */
        post: operations["assignRole"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/members/{userId}/role-assignments/{assignmentId}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post?: never;
        /**
         * Revocar un rol de un miembro
         * @description No se puede revocar el último rol de administrador vigente de la organización (409 last_administrator).
         */
        delete: operations["revokeRole"];
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/organization": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /** Organización activa */
        get: operations["getOrganization"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/platform/organizations": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        /**
         * Crear una organización
         * @description Crea la organización y la invitación de su primer administrador, que se devuelve en administratorInvitation. Solo para el operador de plataforma.
         */
        post: operations["createOrganization"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/platform/organizations/{organizationId}/administrator-invitations": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        /**
         * Emitir una nueva invitación de administrador
         * @description Para cuando la invitación del primer administrador se perdió o venció. Solo para el operador de plataforma.
         */
        post: operations["reissueAdministratorInvitation"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/schedules/{id}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /** Obtener un horario */
        get: operations["getSchedule"];
        /** Modificar un horario */
        put: operations["updateSchedule"];
        post?: never;
        /**
         * Eliminar un horario
         * @description Solo si ningún otorgamiento lo usa (409 schedule_in_use).
         */
        delete: operations["deleteSchedule"];
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/sites": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /**
         * Listar los sitios
         * @description Con alcance de sitio, solo los sitios del alcance.
         */
        get: operations["listSites"];
        put?: never;
        /**
         * Crear un sitio
         * @description Exige sites.manage con alcance de organización.
         */
        post: operations["createSite"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/sites/{id}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /**
         * Obtener un sitio
         * @description Un sitio fuera del alcance responde igual que uno inexistente (404).
         */
        get: operations["getSite"];
        /**
         * Modificar un sitio
         * @description Un sitio visible pero fuera del alcance de sites.manage responde 403; uno no visible, 404.
         */
        put: operations["updateSite"];
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/sites/{siteId}/access-events": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /**
         * Listar los eventos de acceso de un sitio
         * @description Del más reciente al más antiguo por el instante en que ocurrieron, con filtros opcionales por punto, sujeto y rango [from, to). Páginas de hasta 100 (50 por defecto); nextCursor pide la siguiente.
         */
        get: operations["listAccessEvents"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/sites/{siteId}/access-points": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /**
         * Listar los puntos de acceso de un sitio
         * @description Ordenados por nombre. Con alcance de espacio, solo los que dan acceso a ese espacio o a sus descendientes.
         */
        get: operations["listAccessPoints"];
        put?: never;
        /**
         * Crear un punto de acceso
         * @description El espacio al que da acceso y el de origen deben ser del mismo sitio; el modo de fallo es obligatorio. Con alcance de espacio, el espacio al que da acceso tiene que estar en el subárbol del alcance.
         */
        post: operations["createAccessPoint"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/sites/{siteId}/credentials": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /**
         * Listar las credenciales de un sitio
         * @description Opcionalmente, solo las de un sujeto. Sin el valor ni su hash.
         */
        get: operations["listCredentials"];
        put?: never;
        /**
         * Registrar una credencial
         * @description Control RF, tarjeta o PIN de un sujeto del sitio. Se guarda solo el HMAC del valor con la clave del sitio; el valor no se devuelve nunca.
         */
        post: operations["createCredential"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/sites/{siteId}/devices": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /**
         * Listar los dispositivos de un sitio
         * @description Ordenados por nombre, con su estado, sus puntos de acceso, el último contacto y las versiones.
         */
        get: operations["listDevices"];
        put?: never;
        /**
         * Registrar un dispositivo
         * @description Queda pendiente con un código de reclamo de un solo uso que vence a los 7 días. El código solo aparece en esta respuesta.
         */
        post: operations["createDevice"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/sites/{siteId}/grants": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /**
         * Listar los otorgamientos de un sitio
         * @description Opcionalmente, solo los de un sujeto.
         */
        get: operations["listGrants"];
        put?: never;
        /**
         * Crear un otorgamiento
         * @description Sujeto, destino y horario tienen que ser del sitio. El máximo de usos solo vale para permisos.
         */
        post: operations["createGrant"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/sites/{siteId}/schedules": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /** Listar los horarios de un sitio */
        get: operations["listSchedules"];
        put?: never;
        /**
         * Crear un horario
         * @description Ventanas con días (monday … sunday) y horas HH:mm de inicio y fin (fin exclusivo, hasta 24:00).
         */
        post: operations["createSchedule"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/sites/{siteId}/spaces": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /**
         * Listar los espacios de un sitio
         * @description En orden de árbol: cada espacio después de su padre y los hermanos por nombre. Con alcance de espacio, solo ese espacio y sus descendientes.
         */
        get: operations["listSpaces"];
        put?: never;
        /**
         * Crear un espacio
         * @description Sin padre es la raíz del sitio y debe ser de tipo site; un sitio tiene una sola raíz. Con alcance de espacio, el padre tiene que estar en el subárbol del alcance.
         */
        post: operations["createSpace"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/sites/{siteId}/subjects": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /**
         * Listar los sujetos de un sitio
         * @description Ordenados por alias.
         */
        get: operations["listSubjects"];
        put?: never;
        /**
         * Registrar un sujeto
         * @description Persona o anónimo, con un alias. No se cargan datos personales.
         */
        post: operations["createSubject"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/spaces/{id}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /**
         * Obtener un espacio
         * @description Un espacio fuera del alcance responde igual que uno inexistente (404).
         */
        get: operations["getSpace"];
        /**
         * Modificar un espacio
         * @description Cambia nombre, tipo y marca de espacio común; el sitio y el padre no cambian.
         */
        put: operations["updateSpace"];
        post?: never;
        /**
         * Eliminar un espacio
         * @description Solo sin espacios hijos, puntos de acceso relacionados ni roles o invitaciones pendientes que lo tengan como alcance.
         */
        delete: operations["deleteSpace"];
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/spaces/{id}/access-path": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /**
         * Obtener el camino a un espacio
         * @description Puntos de acceso que dan acceso al espacio o a sus ancestros, desde la raíz y, a igual profundidad, por nombre. Con alcance de espacio, el camino se devuelve completo.
         */
        get: operations["getAccessPath"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/subjects/{id}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /** Obtener un sujeto */
        get: operations["getSubject"];
        /** Modificar un sujeto */
        put: operations["updateSubject"];
        post?: never;
        /**
         * Eliminar un sujeto
         * @description Solo sin credenciales ni otorgamientos (409 subject_in_use).
         */
        delete: operations["deleteSubject"];
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
}
export type webhooks = Record<string, never>;
export interface components {
    schemas: {
        AcceptInvitationRequest: {
            password: null | string;
            token: null | string;
        };
        AcceptInvitationResponse: {
            /** Format: uuid */
            organizationId: string;
            /** Format: uuid */
            userId: string;
        };
        AccessEventDto: {
            /** Format: uuid */
            accessPointId: string;
            /** Format: uuid */
            credentialId: null | string;
            decision: string;
            /** Format: uuid */
            deviceId: string;
            /** Format: uuid */
            id: string;
            /** Format: date-time */
            occurredAt: string;
            /** Format: int32 */
            policyVersion: null | number;
            reason: string;
            /** Format: date-time */
            receivedAt: string;
            /** Format: uuid */
            siteId: string;
            /** Format: uuid */
            subjectId: null | string;
        };
        AccessEventPage: {
            items: components["schemas"]["AccessEventDto"][];
            nextCursor: null | string;
        };
        AccessPathEntryDto: {
            failMode: string;
            /** Format: uuid */
            fromSpaceId: null | string;
            /** Format: uuid */
            id: string;
            name: string;
            /** Format: uuid */
            siteId: string;
            /** Format: int32 */
            toSpaceDepth: number;
            /** Format: uuid */
            toSpaceId: string;
            type: string;
        };
        AccessPointDto: {
            failMode: string;
            /** Format: uuid */
            fromSpaceId: null | string;
            /** Format: uuid */
            id: string;
            name: string;
            /** Format: uuid */
            siteId: string;
            /** Format: uuid */
            toSpaceId: string;
            type: string;
        };
        AccessPointRequest: {
            /** @enum {null|string} */
            failMode: "fail_safe" | "fail_secure" | null;
            /** Format: uuid */
            fromSpaceId: null | string;
            name: null | string;
            /** Format: uuid */
            toSpaceId: null | string;
            /** @enum {null|string} */
            type: "door" | "gate" | "barrier" | "turnstile" | "elevator" | null;
        };
        AdministratorInvitationRequest: {
            email: null | string;
        };
        AssignAccessPointsRequest: {
            accessPointIds: null | string[];
        };
        AssignRoleRequest: {
            role: null | string;
            scopeType: null | string;
            /** Format: uuid */
            siteId: null | string;
            /** Format: uuid */
            spaceId: null | string;
            /** Format: date-time */
            validFrom: null | string;
            /** Format: date-time */
            validUntil: null | string;
        };
        ClaimRequest: {
            certificateRequest: null | string;
            claimCode: null | string;
        };
        CompiledPolicy: {
            /** Format: uuid */
            accessPointId: string;
            credentials: components["schemas"]["PolicyCredential"][];
            entries: components["schemas"]["PolicyEntry"][];
            /** Format: int32 */
            format: number;
            hash: null | string;
            offsets: components["schemas"]["PolicyOffset"][];
            /** Format: uuid */
            siteId: string;
            timeZone: string;
            windows: components["schemas"]["PolicyWindow"][][];
        };
        CreateCredentialRequest: {
            /** Format: uuid */
            subjectId: null | string;
            /** @enum {null|string} */
            type: "rf" | "card" | "pin" | null;
            /** Format: date-time */
            validFrom: null | string;
            /** Format: date-time */
            validUntil: null | string;
            value: null | string;
        };
        CreatedOrganizationResponse: {
            administratorInvitation: null | components["schemas"]["IssuedInvitation"];
            /** Format: uuid */
            id: string;
            name: string;
            status: string;
        };
        CreateGrantRequest: {
            /** @enum {null|string} */
            effect: "allow" | "deny" | null;
            /** Format: int32 */
            maxUses: null | number;
            /** Format: uuid */
            scheduleId: null | string;
            /** Format: uuid */
            subjectId: null | string;
            /** Format: uuid */
            targetId: null | string;
            /** @enum {null|string} */
            targetType: "space" | "access_point" | null;
            /** Format: date-time */
            validFrom: null | string;
            /** Format: date-time */
            validUntil: null | string;
        };
        CreateOrganizationRequest: {
            administratorEmail: null | string;
            name: null | string;
        };
        CreateSiteRequest: {
            configuration: null | components["schemas"]["JsonElement"];
            name: null | string;
            timeZone: null | string;
            /** @enum {null|string} */
            type: "building" | "gated_community" | "company" | "house" | null;
        };
        CreateSpaceRequest: {
            isCommon: null | boolean;
            name: null | string;
            /** Format: uuid */
            parentId: null | string;
            /** @enum {null|string} */
            type: "site" | "building" | "floor" | "unit" | "common_area" | "parking" | "lot" | "zone" | null;
        };
        CredentialDto: {
            /** Format: uuid */
            id: string;
            /** Format: uuid */
            siteId: string;
            /** Format: uuid */
            subjectId: string;
            type: string;
            /** Format: date-time */
            validFrom: null | string;
            /** Format: date-time */
            validUntil: null | string;
        };
        CurrentUserInfo: {
            /** Format: uuid */
            activeOrganizationId: null | string;
            email: string;
            /** Format: uuid */
            id: string;
            organizations: components["schemas"]["OrganizationSummary"][];
            platformOperator: boolean;
        };
        DeviceClaim: {
            authorityCertificate: string;
            certificate: string;
            credentialKey: string;
            /** Format: uuid */
            deviceId: string;
            policyKey: string;
            /** Format: int32 */
            pollSeconds: number;
            /** Format: uuid */
            siteId: string;
        };
        DeviceDto: {
            accessPointIds: string[];
            /** Format: date-time */
            certificateExpiresAt: null | string;
            /** Format: date-time */
            claimExpiresAt: null | string;
            firmwareVersion: null | string;
            /** Format: uuid */
            id: string;
            /** Format: date-time */
            lastSeenAt: null | string;
            name: string;
            /** Format: int32 */
            policyVersion: null | number;
            /** Format: int32 */
            reportedPolicyVersion: null | number;
            /** Format: uuid */
            siteId: string;
            status: string;
        };
        DeviceEventRequest: {
            /** Format: uuid */
            accessPointId: null | string;
            credential: null | string;
            decision: null | string;
            /** Format: uuid */
            id: null | string;
            /** Format: date-time */
            occurredAt: null | string;
            /** Format: int32 */
            policyVersion: null | number;
            reason: null | string;
        };
        DeviceEventsRequest: {
            events: null | components["schemas"]["DeviceEventRequest"][];
        };
        DevicePolicyResponse: {
            /** Format: uuid */
            deviceId: string;
            /** Format: int32 */
            format: number;
            /** Format: date-time */
            issuedAt: string;
            points: components["schemas"]["CompiledPolicy"][];
            /** Format: uuid */
            siteId: string;
            /** Format: int32 */
            version: number;
        };
        DeviceRequest: {
            name: null | string;
        };
        GrantDto: {
            effect: string;
            /** Format: uuid */
            id: string;
            origin: string;
            /** Format: int32 */
            remainingUses: null | number;
            /** Format: uuid */
            scheduleId: null | string;
            /** Format: uuid */
            siteId: string;
            /** Format: uuid */
            subjectId: string;
            /** Format: uuid */
            targetId: string;
            targetType: string;
            /** Format: date-time */
            validFrom: null | string;
            /** Format: date-time */
            validUntil: null | string;
        };
        HeartbeatRequest: {
            firmwareVersion: null | string;
            /** Format: int32 */
            policyVersion: null | number;
        };
        HeartbeatResult: {
            /** Format: int32 */
            policyVersion: number;
            /** Format: int32 */
            pollSeconds: number;
            /** Format: date-time */
            serverTime: string;
        };
        HttpValidationProblemDetails: {
            /** @description Código de error estable, para que el cliente decida qué hacer (por ejemplo, permission_denied). */
            code: string;
            /** @description Identificador de correlación de la solicitud; también viene en el encabezado X-Correlation-Id. */
            correlationId: string;
            detail?: null | string;
            errors?: {
                [key: string]: string[];
            };
            instance?: null | string;
            /** Format: int32 */
            status: null | number;
            title: null | string;
            type?: null | string;
        };
        IngestResult: {
            /** Format: int32 */
            accepted: number;
            /** Format: int32 */
            duplicates: number;
        };
        InviteRequest: {
            email: null | string;
            role: null | string;
            scopeType: null | string;
            /** Format: uuid */
            siteId: null | string;
            /** Format: uuid */
            spaceId: null | string;
        };
        IssuedDevice: {
            claimCode: string;
            /** Format: date-time */
            claimExpiresAt: string;
            device: components["schemas"]["DeviceDto"];
        };
        IssuedInvitation: {
            email: string;
            /** Format: date-time */
            expiresAt: string;
            /** Format: uuid */
            id: string;
            role: string;
            scopeType: string;
            /** Format: uuid */
            siteId: null | string;
            /** Format: uuid */
            spaceId: null | string;
            token: string;
        };
        JsonElement: unknown;
        MemberResponse: {
            email: string;
            roleAssignments: components["schemas"]["RoleAssignmentResponse"][];
            /** Format: uuid */
            userId: string;
        };
        OrganizationDto: {
            /** Format: uuid */
            id: string;
            name: string;
            status: string;
        };
        OrganizationSummary: {
            /** Format: uuid */
            id: string;
            name: string;
        };
        PermissionResponse: {
            name: string;
            organization: boolean;
            siteIds: string[];
            spaces: components["schemas"]["PermissionSpaceResponse"][];
        };
        PermissionSpaceResponse: {
            /** Format: uuid */
            siteId: string;
            /** Format: uuid */
            spaceId: string;
        };
        PermissionsResponse: {
            /** Format: uuid */
            organizationId: null | string;
            permissions: components["schemas"]["PermissionResponse"][];
        };
        PolicyCredential: {
            /** Format: int64 */
            from: null | number;
            hash: string;
            /** Format: int64 */
            to: null | number;
        };
        PolicyEntry: {
            /** Format: int32 */
            credential: number;
            deny: boolean;
            /** Format: int64 */
            from: null | number;
            /** Format: int64 */
            to: null | number;
            /** Format: int32 */
            uses: null | number;
            /** Format: int32 */
            window: null | number;
        };
        PolicyOffset: {
            /** Format: int64 */
            from: null | number;
            /** Format: int32 */
            seconds: number;
        };
        PolicyWindow: {
            /** Format: int32 */
            days: number;
            /** Format: int32 */
            from: number;
            /** Format: int32 */
            to: number;
        };
        ProblemDetails: {
            /** @description Código de error estable, para que el cliente decida qué hacer (por ejemplo, permission_denied). */
            code: string;
            /** @description Identificador de correlación de la solicitud; también viene en el encabezado X-Correlation-Id. */
            correlationId: string;
            detail?: null | string;
            instance?: null | string;
            /** Format: int32 */
            status: null | number;
            title: null | string;
            type?: null | string;
        };
        RefreshRequest: {
            /** Format: uuid */
            organizationId: null | string;
            refreshToken: null | string;
        };
        RoleAssignmentResponse: {
            /** Format: uuid */
            id: string;
            role: string;
            scopeType: string;
            /** Format: uuid */
            siteId: null | string;
            /** Format: uuid */
            spaceId: null | string;
            /** Format: date-time */
            validFrom: null | string;
            /** Format: date-time */
            validUntil: null | string;
        };
        ScheduleDto: {
            /** Format: uuid */
            id: string;
            name: string;
            /** Format: uuid */
            siteId: string;
            windows: components["schemas"]["ScheduleWindowDto"][];
        };
        ScheduleRequest: {
            name: null | string;
            windows: null | components["schemas"]["ScheduleWindowRequest"][];
        };
        ScheduleWindowDto: {
            days: string[];
            from: string;
            to: string;
        };
        ScheduleWindowRequest: {
            days: null | string[];
            from: null | string;
            to: null | string;
        };
        SignInRequest: {
            email: null | string;
            /** Format: uuid */
            organizationId: null | string;
            password: null | string;
            refreshTokenInCookie?: null | boolean;
        };
        SignOutRequest: {
            refreshToken: null | string;
        };
        SimulationRequest: {
            /** Format: date-time */
            at: null | string;
            /** @enum {null|string} */
            credentialType: "rf" | "card" | "pin" | null;
            credentialValue: null | string;
        };
        SimulationResultDto: {
            decision: string;
            policyHash: string;
            reason: string;
        };
        SiteDto: {
            configuration: components["schemas"]["JsonElement"];
            /** Format: date-time */
            createdAt: string;
            /** Format: uuid */
            id: string;
            name: string;
            /** Format: uuid */
            organizationId: string;
            timeZone: string;
            type: string;
            /** Format: date-time */
            updatedAt: string;
        };
        SpaceDto: {
            ancestorIds: string[];
            /** Format: int32 */
            depth: number;
            /** Format: uuid */
            id: string;
            isCommon: boolean;
            name: string;
            /** Format: uuid */
            parentId: null | string;
            /** Format: uuid */
            siteId: string;
            type: string;
        };
        SubjectDto: {
            alias: string;
            /** Format: uuid */
            id: string;
            kind: string;
            /** Format: uuid */
            siteId: string;
        };
        SubjectRequest: {
            alias: null | string;
            /** @enum {null|string} */
            kind: "person" | "anonymous" | null;
        };
        TokenResponse: {
            accessToken: string;
            /** Format: int32 */
            expiresIn: number;
            /** Format: uuid */
            organizationId: null | string;
            refreshToken: null | string;
            tokenType: string;
        };
        UpdateCredentialRequest: {
            /** Format: date-time */
            validFrom: null | string;
            /** Format: date-time */
            validUntil: null | string;
        };
        UpdateSiteRequest: {
            configuration: null | components["schemas"]["JsonElement"];
            name: null | string;
            timeZone: null | string;
        };
        UpdateSpaceRequest: {
            isCommon: null | boolean;
            name: null | string;
            /** @enum {null|string} */
            type: "site" | "building" | "floor" | "unit" | "common_area" | "parking" | "lot" | "zone" | null;
        };
    };
    responses: never;
    parameters: never;
    requestBodies: never;
    headers: never;
    pathItems: never;
}
export type $defs = Record<string, never>;
export interface operations {
    getAccessPoint: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description Operación correcta. */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["AccessPointDto"];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description El recurso no existe o no es visible para el usuario. */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    updateAccessPoint: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["AccessPointRequest"];
            };
        };
        responses: {
            /** @description Operación correcta. */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["AccessPointDto"];
                };
            };
            /** @description Solicitud inválida: el detalle de cada campo viene en errors. */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description El recurso no existe o no es visible para el usuario. */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflicto con el estado actual; el código indica cuál. */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    deleteAccessPoint: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description Operación correcta, sin contenido. */
            204: {
                headers: {
                    [name: string]: unknown;
                };
                content?: never;
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description El recurso no existe o no es visible para el usuario. */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    getAccessPointPolicy: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description Operación correcta. */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["CompiledPolicy"];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description El recurso no existe o no es visible para el usuario. */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    simulateAccess: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["SimulationRequest"];
            };
        };
        responses: {
            /** @description Operación correcta. */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["SimulationResultDto"];
                };
            };
            /** @description Solicitud inválida: el detalle de cada campo viene en errors. */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description El recurso no existe o no es visible para el usuario. */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    signOut: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["SignOutRequest"];
            };
        };
        responses: {
            /** @description Operación correcta, sin contenido. */
            204: {
                headers: {
                    [name: string]: unknown;
                };
                content?: never;
            };
            /** @description Solicitud inválida: el detalle de cada campo viene en errors. */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    refreshToken: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["RefreshRequest"];
            };
        };
        responses: {
            /** @description Operación correcta. */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["TokenResponse"];
                };
            };
            /** @description Solicitud inválida: el detalle de cada campo viene en errors. */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    signIn: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["SignInRequest"];
            };
        };
        responses: {
            /** @description Operación correcta. */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["TokenResponse"];
                };
            };
            /** @description Solicitud inválida: el detalle de cada campo viene en errors. */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Demasiados intentos; reintentar más tarde (too_many_requests). */
            429: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    getCredential: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description Operación correcta. */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["CredentialDto"];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description El recurso no existe o no es visible para el usuario. */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    updateCredential: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["UpdateCredentialRequest"];
            };
        };
        responses: {
            /** @description Operación correcta. */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["CredentialDto"];
                };
            };
            /** @description Solicitud inválida: el detalle de cada campo viene en errors. */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description El recurso no existe o no es visible para el usuario. */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    deleteCredential: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description Operación correcta, sin contenido. */
            204: {
                headers: {
                    [name: string]: unknown;
                };
                content?: never;
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description El recurso no existe o no es visible para el usuario. */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    claimDevice: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["ClaimRequest"];
            };
        };
        responses: {
            /** @description Operación correcta. */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["DeviceClaim"];
                };
            };
            /** @description Solicitud inválida: el detalle de cada campo viene en errors. */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Demasiados intentos; reintentar más tarde (too_many_requests). */
            429: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    sendDeviceEvents: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["DeviceEventsRequest"];
            };
        };
        responses: {
            /** @description Operación correcta. */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["IngestResult"];
                };
            };
            /** @description Solicitud inválida: el detalle de cada campo viene en errors. */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    deviceHeartbeat: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["HeartbeatRequest"];
            };
        };
        responses: {
            /** @description Operación correcta. */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["HeartbeatResult"];
                };
            };
            /** @description Solicitud inválida: el detalle de cada campo viene en errors. */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    getDevicePolicy: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description Operación correcta. */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["DevicePolicyResponse"];
                };
            };
            /** @description Sin cambios: el cliente ya tiene la versión vigente. */
            304: {
                headers: {
                    [name: string]: unknown;
                };
                content?: never;
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    getDevice: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description Operación correcta. */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["DeviceDto"];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description El recurso no existe o no es visible para el usuario. */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    updateDevice: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["DeviceRequest"];
            };
        };
        responses: {
            /** @description Operación correcta. */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["DeviceDto"];
                };
            };
            /** @description Solicitud inválida: el detalle de cada campo viene en errors. */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description El recurso no existe o no es visible para el usuario. */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflicto con el estado actual; el código indica cuál. */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    deleteDevice: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description Operación correcta, sin contenido. */
            204: {
                headers: {
                    [name: string]: unknown;
                };
                content?: never;
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description El recurso no existe o no es visible para el usuario. */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    assignDeviceAccessPoints: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["AssignAccessPointsRequest"];
            };
        };
        responses: {
            /** @description Operación correcta. */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["DeviceDto"];
                };
            };
            /** @description Solicitud inválida: el detalle de cada campo viene en errors. */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description El recurso no existe o no es visible para el usuario. */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflicto con el estado actual; el código indica cuál. */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    issueDeviceClaimCode: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description Operación correcta. */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["IssuedDevice"];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description El recurso no existe o no es visible para el usuario. */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    revokeDevice: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description Operación correcta. */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["DeviceDto"];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description El recurso no existe o no es visible para el usuario. */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    getGrant: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description Operación correcta. */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["GrantDto"];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description El recurso no existe o no es visible para el usuario. */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    deleteGrant: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description Operación correcta, sin contenido. */
            204: {
                headers: {
                    [name: string]: unknown;
                };
                content?: never;
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description El recurso no existe o no es visible para el usuario. */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    createInvitation: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["InviteRequest"];
            };
        };
        responses: {
            /** @description Recurso creado. */
            201: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["IssuedInvitation"];
                };
            };
            /** @description Solicitud inválida: el detalle de cada campo viene en errors. */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflicto con el estado actual; el código indica cuál. */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    acceptInvitation: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["AcceptInvitationRequest"];
            };
        };
        responses: {
            /** @description Operación correcta. */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["AcceptInvitationResponse"];
                };
            };
            /** @description Solicitud inválida: el detalle de cada campo viene en errors. */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflicto con el estado actual; el código indica cuál. */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Demasiados intentos; reintentar más tarde (too_many_requests). */
            429: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    getCurrentUser: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description Operación correcta. */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["CurrentUserInfo"];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    getMyPermissions: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description Operación correcta. */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["PermissionsResponse"];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    listMembers: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description Operación correcta. */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["MemberResponse"][];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    removeMember: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                userId: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description Operación correcta, sin contenido. */
            204: {
                headers: {
                    [name: string]: unknown;
                };
                content?: never;
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description El recurso no existe o no es visible para el usuario. */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflicto con el estado actual; el código indica cuál. */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    assignRole: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                userId: string;
            };
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["AssignRoleRequest"];
            };
        };
        responses: {
            /** @description Recurso creado. */
            201: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["RoleAssignmentResponse"];
                };
            };
            /** @description Solicitud inválida: el detalle de cada campo viene en errors. */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description El recurso no existe o no es visible para el usuario. */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflicto con el estado actual; el código indica cuál. */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    revokeRole: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                assignmentId: string;
                userId: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description Operación correcta, sin contenido. */
            204: {
                headers: {
                    [name: string]: unknown;
                };
                content?: never;
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description El recurso no existe o no es visible para el usuario. */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflicto con el estado actual; el código indica cuál. */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    getOrganization: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description Operación correcta. */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["OrganizationDto"];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description El recurso no existe o no es visible para el usuario. */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    createOrganization: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["CreateOrganizationRequest"];
            };
        };
        responses: {
            /** @description Recurso creado. */
            201: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["CreatedOrganizationResponse"];
                };
            };
            /** @description Solicitud inválida: el detalle de cada campo viene en errors. */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflicto con el estado actual; el código indica cuál. */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    reissueAdministratorInvitation: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                organizationId: string;
            };
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["AdministratorInvitationRequest"];
            };
        };
        responses: {
            /** @description Recurso creado. */
            201: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["IssuedInvitation"];
                };
            };
            /** @description Solicitud inválida: el detalle de cada campo viene en errors. */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description El recurso no existe o no es visible para el usuario. */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    getSchedule: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description Operación correcta. */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["ScheduleDto"];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description El recurso no existe o no es visible para el usuario. */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    updateSchedule: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["ScheduleRequest"];
            };
        };
        responses: {
            /** @description Operación correcta. */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["ScheduleDto"];
                };
            };
            /** @description Solicitud inválida: el detalle de cada campo viene en errors. */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description El recurso no existe o no es visible para el usuario. */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflicto con el estado actual; el código indica cuál. */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    deleteSchedule: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description Operación correcta, sin contenido. */
            204: {
                headers: {
                    [name: string]: unknown;
                };
                content?: never;
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description El recurso no existe o no es visible para el usuario. */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflicto con el estado actual; el código indica cuál. */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    listSites: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description Operación correcta. */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["SiteDto"][];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    createSite: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["CreateSiteRequest"];
            };
        };
        responses: {
            /** @description Recurso creado. */
            201: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["SiteDto"];
                };
            };
            /** @description Solicitud inválida: el detalle de cada campo viene en errors. */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflicto con el estado actual; el código indica cuál. */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    getSite: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description Operación correcta. */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["SiteDto"];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description El recurso no existe o no es visible para el usuario. */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    updateSite: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["UpdateSiteRequest"];
            };
        };
        responses: {
            /** @description Operación correcta. */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["SiteDto"];
                };
            };
            /** @description Solicitud inválida: el detalle de cada campo viene en errors. */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description El recurso no existe o no es visible para el usuario. */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflicto con el estado actual; el código indica cuál. */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    listAccessEvents: {
        parameters: {
            query?: {
                accessPointId?: string;
                cursor?: string;
                from?: string;
                limit?: number;
                subjectId?: string;
                to?: string;
            };
            header?: never;
            path: {
                siteId: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description Operación correcta. */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["AccessEventPage"];
                };
            };
            /** @description Solicitud inválida: el detalle de cada campo viene en errors. */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description El recurso no existe o no es visible para el usuario. */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    listAccessPoints: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                siteId: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description Operación correcta. */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["AccessPointDto"][];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description El recurso no existe o no es visible para el usuario. */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    createAccessPoint: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                siteId: string;
            };
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["AccessPointRequest"];
            };
        };
        responses: {
            /** @description Recurso creado. */
            201: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["AccessPointDto"];
                };
            };
            /** @description Solicitud inválida: el detalle de cada campo viene en errors. */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description El recurso no existe o no es visible para el usuario. */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflicto con el estado actual; el código indica cuál. */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    listCredentials: {
        parameters: {
            query?: {
                subjectId?: string;
            };
            header?: never;
            path: {
                siteId: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description Operación correcta. */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["CredentialDto"][];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description El recurso no existe o no es visible para el usuario. */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    createCredential: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                siteId: string;
            };
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["CreateCredentialRequest"];
            };
        };
        responses: {
            /** @description Recurso creado. */
            201: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["CredentialDto"];
                };
            };
            /** @description Solicitud inválida: el detalle de cada campo viene en errors. */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description El recurso no existe o no es visible para el usuario. */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflicto con el estado actual; el código indica cuál. */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    listDevices: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                siteId: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description Operación correcta. */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["DeviceDto"][];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description El recurso no existe o no es visible para el usuario. */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    createDevice: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                siteId: string;
            };
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["DeviceRequest"];
            };
        };
        responses: {
            /** @description Recurso creado. */
            201: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["IssuedDevice"];
                };
            };
            /** @description Solicitud inválida: el detalle de cada campo viene en errors. */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description El recurso no existe o no es visible para el usuario. */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflicto con el estado actual; el código indica cuál. */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    listGrants: {
        parameters: {
            query?: {
                subjectId?: string;
            };
            header?: never;
            path: {
                siteId: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description Operación correcta. */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["GrantDto"][];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description El recurso no existe o no es visible para el usuario. */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    createGrant: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                siteId: string;
            };
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["CreateGrantRequest"];
            };
        };
        responses: {
            /** @description Recurso creado. */
            201: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["GrantDto"];
                };
            };
            /** @description Solicitud inválida: el detalle de cada campo viene en errors. */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description El recurso no existe o no es visible para el usuario. */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    listSchedules: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                siteId: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description Operación correcta. */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["ScheduleDto"][];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description El recurso no existe o no es visible para el usuario. */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    createSchedule: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                siteId: string;
            };
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["ScheduleRequest"];
            };
        };
        responses: {
            /** @description Recurso creado. */
            201: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["ScheduleDto"];
                };
            };
            /** @description Solicitud inválida: el detalle de cada campo viene en errors. */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description El recurso no existe o no es visible para el usuario. */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflicto con el estado actual; el código indica cuál. */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    listSpaces: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                siteId: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description Operación correcta. */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["SpaceDto"][];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description El recurso no existe o no es visible para el usuario. */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    createSpace: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                siteId: string;
            };
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["CreateSpaceRequest"];
            };
        };
        responses: {
            /** @description Recurso creado. */
            201: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["SpaceDto"];
                };
            };
            /** @description Solicitud inválida: el detalle de cada campo viene en errors. */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description El recurso no existe o no es visible para el usuario. */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflicto con el estado actual; el código indica cuál. */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    listSubjects: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                siteId: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description Operación correcta. */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["SubjectDto"][];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description El recurso no existe o no es visible para el usuario. */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    createSubject: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                siteId: string;
            };
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["SubjectRequest"];
            };
        };
        responses: {
            /** @description Recurso creado. */
            201: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["SubjectDto"];
                };
            };
            /** @description Solicitud inválida: el detalle de cada campo viene en errors. */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description El recurso no existe o no es visible para el usuario. */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    getSpace: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description Operación correcta. */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["SpaceDto"];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description El recurso no existe o no es visible para el usuario. */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    updateSpace: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["UpdateSpaceRequest"];
            };
        };
        responses: {
            /** @description Operación correcta. */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["SpaceDto"];
                };
            };
            /** @description Solicitud inválida: el detalle de cada campo viene en errors. */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description El recurso no existe o no es visible para el usuario. */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflicto con el estado actual; el código indica cuál. */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    deleteSpace: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description Operación correcta, sin contenido. */
            204: {
                headers: {
                    [name: string]: unknown;
                };
                content?: never;
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description El recurso no existe o no es visible para el usuario. */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflicto con el estado actual; el código indica cuál. */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    getAccessPath: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description Operación correcta. */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["AccessPathEntryDto"][];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description El recurso no existe o no es visible para el usuario. */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    getSubject: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description Operación correcta. */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["SubjectDto"];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description El recurso no existe o no es visible para el usuario. */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    updateSubject: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["SubjectRequest"];
            };
        };
        responses: {
            /** @description Operación correcta. */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["SubjectDto"];
                };
            };
            /** @description Solicitud inválida: el detalle de cada campo viene en errors. */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description El recurso no existe o no es visible para el usuario. */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    deleteSubject: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description Operación correcta, sin contenido. */
            204: {
                headers: {
                    [name: string]: unknown;
                };
                content?: never;
            };
            /** @description Falta el token de acceso, es inválido o venció (authentication_required), o las credenciales no son válidas. */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Sin organización activa (organization_context_required) o sin permiso para la operación (permission_denied). */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description El recurso no existe o no es visible para el usuario. */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflicto con el estado actual; el código indica cuál. */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Error inesperado; el identificador de correlación sirve para buscarlo en los logs. */
            500: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
}
